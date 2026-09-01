<#
.SYNOPSIS
    Smokes a deployed Coupon Service stack through the APIM gateway.

.DESCRIPTION
    Verifies what a hardened deployment can honestly answer: that it is serving, that the
    startup seed converged (AC-9.5, AC-9.6), that the gateway rejects an unauthenticated
    call (AC-7.6, AC-9.7), and that a seeded policy still prices a basket correctly when
    reached end to end through APIM.

    The behavioural suite is not run here. The Reqnroll scenarios drive a MutableClock and
    seed run-scoped policies through the admin API, neither of which exists on a deployed
    service, so they run in process in CI instead. See
    .kiro/specs/deployed-stack-smoke/bugfix.md.

    Requires PowerShell 7 for -SkipHttpErrorCheck.

.PARAMETER CouponBaseUrl
    Coupon Service base URL including the gateway path, for example
    https://apim-coupon-dev-xxxx.azure-api.net/coupons

.PARAMETER OrderBaseUrl
    Order API base URL including the gateway path, for example
    https://apim-coupon-dev-xxxx.azure-api.net/orders

.PARAMETER Audience
    Application ID URI or client id the token is requested for, for example api://coupon-service.

.PARAMETER BearerToken
    Pre-acquired token. Omit to acquire one from the signed-in Azure CLI identity, which in
    CD is the workload-identity federation service principal.

.PARAMETER ReadyTimeoutSeconds
    How long to wait for the deployment to report healthy before failing.

.EXAMPLE
    ./scripts/smoke-deployed-stack.ps1 `
        -CouponBaseUrl https://apim-coupon-dev-x.azure-api.net/coupons `
        -OrderBaseUrl https://apim-coupon-dev-x.azure-api.net/orders `
        -Audience api://coupon-service
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $CouponBaseUrl,

    [Parameter(Mandatory = $true)]
    [string] $OrderBaseUrl,

    [Parameter(Mandatory = $true)]
    [string] $Audience,

    [string] $BearerToken,

    [int] $ReadyTimeoutSeconds = 300
)

$ErrorActionPreference = 'Stop'

$script:Failures = [System.Collections.Generic.List[string]]::new()

function Get-AbsoluteBase {
    param(
        [string] $Name,
        [string] $Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "$Name was not supplied."
    }

    $trimmed = $Value.Trim().TrimEnd('/')
    $uri = $null
    if (-not [Uri]::TryCreate($trimmed, [UriKind]::Absolute, [ref] $uri) -or
        ($uri.Scheme -ne [Uri]::UriSchemeHttp -and $uri.Scheme -ne [Uri]::UriSchemeHttps)) {
        throw "$Name is not an absolute http(s) URI: '$Value'"
    }

    return $trimmed
}

function Invoke-Probe {
    param(
        [string] $Method,
        [string] $Uri,
        [string] $Body,
        [string] $Token
    )

    $headers = @{}
    if (-not [string]::IsNullOrWhiteSpace($Token)) {
        $headers['Authorization'] = "Bearer $Token"
    }

    $arguments = @{
        Uri                = $Uri
        Method             = $Method
        Headers            = $headers
        SkipHttpErrorCheck = $true
        StatusCodeVariable = 'status'
        ErrorAction        = 'Stop'
    }

    if (-not [string]::IsNullOrWhiteSpace($Body)) {
        $arguments['Body'] = $Body
        $arguments['ContentType'] = 'application/json'
    }

    $status = 0
    $response = Invoke-RestMethod @arguments

    return [pscustomobject]@{
        Status = [int] $status
        Body   = $response
    }
}

function Assert-Status {
    param(
        [string] $Name,
        [string] $Method,
        [string] $Uri,
        [int] $Expected,
        [string] $Body,
        [string] $Token
    )

    $result = Invoke-Probe -Method $Method -Uri $Uri -Body $Body -Token $Token

    if ($result.Status -eq $Expected) {
        Write-Host "  PASS  $Name (HTTP $($result.Status))"
    }
    else {
        $detail = if ($null -ne $result.Body) { ($result.Body | ConvertTo-Json -Compress -Depth 6) } else { '<empty>' }
        Write-Host "  FAIL  $Name : expected HTTP $Expected, got $($result.Status). $detail"
        $script:Failures.Add("$Name (expected $Expected, got $($result.Status))")
    }

    return $result
}

function Wait-ForHealthy {
    param(
        [string] $Uri,
        [int] $TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $attempt = 0

    while ((Get-Date) -lt $deadline) {
        $attempt++
        $result = Invoke-Probe -Method 'Get' -Uri $Uri
        if ($result.Status -eq 200) {
            Write-Host "  PASS  readiness reported healthy after $attempt attempt(s)"
            return
        }

        Write-Host "  ....  attempt $attempt : HTTP $($result.Status); retrying"
        Start-Sleep -Seconds 10
    }

    $script:Failures.Add("readiness never reported healthy within $TimeoutSeconds seconds ($Uri)")
    Write-Host "  FAIL  readiness never reported healthy within $TimeoutSeconds seconds"
}

function Get-Token {
    param([string] $Resource)

    $output = (az account get-access-token --resource $Resource --query accessToken -o tsv 2>&1 |
        ForEach-Object { $_.ToString() }) -join '; '

    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($output)) {
        throw "Could not acquire a token for $Resource. Run scripts/setup-entra-app.ps1 so the " +
              "app registration and its service principal exist. Azure CLI said: $output"
    }

    return $output.Trim()
}

$coupon = Get-AbsoluteBase -Name 'CouponBaseUrl' -Value $CouponBaseUrl
$order = Get-AbsoluteBase -Name 'OrderBaseUrl' -Value $OrderBaseUrl

Write-Host "Smoking $coupon"

Write-Host 'Deployment is serving and the startup seed converged (AC-9.5, AC-9.6)'
$null = Assert-Status -Name 'health/live is anonymous and healthy' `
    -Method 'Get' -Uri "$coupon/v1/health/live" -Expected 200
Wait-ForHealthy -Uri "$coupon/v1/health/ready" -TimeoutSeconds $ReadyTimeoutSeconds

Write-Host 'Gateway rejects an unauthenticated call (AC-7.6, AC-9.7)'
# A basket that would otherwise price successfully, so a 200 here would mean the gateway let it through.
$previewBody = @{
    code                = 'SAVE10'
    customerId          = 'smoke-customer'
    confirmedOrderCount = 0
    cart                = @{
        lines = @(
            @{
                lineId    = 'line-1'
                pizzaId   = 'margherita'
                category  = 'classic'
                unitPrice = 20.00
                quantity  = 2
            }
        )
    }
} | ConvertTo-Json -Depth 6 -Compress

$null = Assert-Status -Name 'preview without a token is rejected' `
    -Method 'Post' -Uri "$coupon/v1/coupons/preview" -Expected 401 -Body $previewBody
$null = Assert-Status -Name 'order pizzas without a token is rejected' `
    -Method 'Get' -Uri "$order/v1/pizzas" -Expected 401

Write-Host 'A seeded policy prices a basket through the gateway'
if ([string]::IsNullOrWhiteSpace($BearerToken)) {
    $BearerToken = Get-Token -Resource $Audience
    Write-Host "  ....  token acquired for $Audience"
}

$preview = Assert-Status -Name 'preview with a token is accepted' `
    -Method 'Post' -Uri "$coupon/v1/coupons/preview" -Expected 200 -Body $previewBody -Token $BearerToken

if ($preview.Status -eq 200) {
    # SAVE10 takes 10 percent of a 40.00 basket. Asserting the money, not just the status code,
    # is what makes this a test of the seeded policy rather than of routing.
    $pricing = $preview.Body.pricing
    $expected = @{ subtotal = [decimal] 40.00; discount = [decimal] 4.00; total = [decimal] 36.00 }

    foreach ($field in @('subtotal', 'discount', 'total')) {
        $actual = [decimal] $pricing.$field
        if ($actual -eq $expected[$field]) {
            Write-Host "  PASS  $field is $actual"
        }
        else {
            Write-Host "  FAIL  $field : expected $($expected[$field]), got $actual"
            $script:Failures.Add("preview $field (expected $($expected[$field]), got $actual)")
        }
    }

    $status = [string] $preview.Body.status
    if ($status -eq 'Applied') {
        Write-Host "  PASS  status is Applied"
    }
    else {
        Write-Host "  FAIL  status : expected Applied, got '$status'"
        $script:Failures.Add("preview status (expected Applied, got '$status')")
    }
}

Write-Host ''
if ($script:Failures.Count -gt 0) {
    Write-Host "Smoke failed with $($script:Failures.Count) problem(s):"
    foreach ($failure in $script:Failures) {
        Write-Host "  - $failure"
    }

    throw "Deployed stack smoke failed: $($script:Failures.Count) check(s) did not pass."
}

Write-Host 'Deployed stack smoke passed.'
