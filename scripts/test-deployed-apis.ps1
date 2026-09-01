<#
.SYNOPSIS
    End-to-end manual test of a deployed develop or prod stack through APIM.

.DESCRIPTION
    Pass the APIM gateway root URL (apimGatewayUrl). The script derives /coupons and /orders
    bases, then walks the happy path:

      1. Anonymous health (live + ready)
      2. Gateway rejects unauthenticated preview and catalog (401)
      3. Acquire Entra token (or use -BearerToken)
      4. Coupon preview with a seeded code (default SAVE10)
      5. Pizza catalog
      6. Place an order (server re-prices; ignores clientTotal)
      7. Fetch the order by id

    Works on Windows PowerShell 5.1 and PowerShell 7+.

.PARAMETER GatewayUrl
    APIM gateway root, for example https://apim-coupon-dev-xxxx.azure-api.net
    Trailing slashes are ignored. Do not include /coupons or /orders.

.PARAMETER Audience
    Token resource for az account get-access-token. Default api://coupon-service.

.PARAMETER BearerToken
    Pre-acquired JWT. When omitted, the script uses the signed-in Azure CLI identity.

.PARAMETER CouponCode
    Seeded coupon code for preview and checkout. Default SAVE10.

.PARAMETER SkipOrder
    Run health, auth, preview and catalog only; skip checkout.

.PARAMETER ReadyTimeoutSeconds
    How long to poll /v1/health/ready before failing.

.EXAMPLE
    ./scripts/test-deployed-apis.ps1 -GatewayUrl https://apim-coupon-dev-xxxx.azure-api.net

.EXAMPLE
    ./scripts/test-deployed-apis.ps1 -GatewayUrl https://apim-coupon-prod-xxxx.azure-api.net -CouponCode FLAT5

.EXAMPLE
    $g = az apim list -g rg-coupon-demo --query "[0].gatewayUrl" -o tsv
    ./scripts/test-deployed-apis.ps1 -GatewayUrl $g
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $GatewayUrl = 'https://apim-coupon-dev-v29r4hxkv774j.azure-api.net',

    [string] $Audience = 'api://coupon-service',

    [string] $BearerToken,

    [string] $CouponCode = 'SAVE10',

    [switch] $SkipOrder,

    [int] $ReadyTimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'

$script:Failures = [System.Collections.Generic.List[string]]::new()
$script:Passes = 0

function Write-Step {
    param([string] $Message)
    Write-Host ''
    Write-Host "== $Message"
}

function Write-Pass {
    param([string] $Message)
    Write-Host "  PASS  $Message" -ForegroundColor Green
    $script:Passes++
}

function Write-Fail {
    param([string] $Message)
    Write-Host "  FAIL  $Message" -ForegroundColor Red
    $script:Failures.Add($Message)
}

function Get-NormalizedGateway {
    param([string] $Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw 'GatewayUrl was not supplied.'
    }

    $trimmed = $Value.Trim().TrimEnd('/')
    $uri = $null
    if (-not [Uri]::TryCreate($trimmed, [UriKind]::Absolute, [ref] $uri) -or
        ($uri.Scheme -ne [Uri]::UriSchemeHttp -and $uri.Scheme -ne [Uri]::UriSchemeHttps)) {
        throw "GatewayUrl is not an absolute http(s) URI: '$Value'"
    }

    if ($trimmed -match '/(coupons|orders|admin)(/|$)') {
        throw "GatewayUrl must be the APIM gateway root only (no /coupons or /orders suffix). Got: '$Value'"
    }

    return $trimmed
}

function Invoke-HttpProbe {
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

    if ($PSVersionTable.PSVersion.Major -ge 7) {
        $arguments = @{
            Uri                = $Uri
            Method             = $Method
            Headers            = $headers
            SkipHttpErrorCheck = $true
            StatusCodeVariable = 'statusCode'
            ErrorAction        = 'Stop'
        }
        if (-not [string]::IsNullOrWhiteSpace($Body)) {
            $arguments['Body'] = $Body
            $arguments['ContentType'] = 'application/json'
        }

        $status = 0
        $response = Invoke-RestMethod @arguments
        return [pscustomobject]@{
            Status = [int] $statusCode
            Body   = $response
        }
    }

    $arguments = @{
        Uri             = $Uri
        Method          = $Method
        Headers         = $headers
        UseBasicParsing = $true
        ErrorAction     = 'Stop'
    }
    if (-not [string]::IsNullOrWhiteSpace($Body)) {
        $arguments['Body'] = $Body
        $arguments['ContentType'] = 'application/json'
    }

    try {
        $web = Invoke-WebRequest @arguments
        $parsed = $null
        if (-not [string]::IsNullOrWhiteSpace($web.Content)) {
            $parsed = $web.Content | ConvertFrom-Json
        }

        return [pscustomobject]@{
            Status = [int] $web.StatusCode
            Body   = $parsed
        }
    }
    catch {
        $web = $_.Exception.Response
        if ($null -eq $web) {
            throw
        }

        $status = [int] $web.StatusCode
        $stream = $web.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $raw = $reader.ReadToEnd()
        $reader.Close()
        $stream.Close()

        $parsed = $null
        if (-not [string]::IsNullOrWhiteSpace($raw)) {
            try {
                $parsed = $raw | ConvertFrom-Json
            }
            catch {
                $parsed = $raw
            }
        }

        return [pscustomobject]@{
            Status = $status
            Body   = $parsed
        }
    }
}

function Assert-HttpStatus {
    param(
        [string] $Name,
        [string] $Method,
        [string] $Uri,
        [int[]] $Expected,
        [string] $Body,
        [string] $Token
    )

    $result = Invoke-HttpProbe -Method $Method -Uri $Uri -Body $Body -Token $Token
    if ($Expected -contains $result.Status) {
        Write-Pass "$Name (HTTP $($result.Status))"
    }
    else {
        $detail = if ($null -ne $result.Body) {
            ($result.Body | ConvertTo-Json -Compress -Depth 6)
        }
        else {
            '<empty>'
        }
        Write-Fail "$Name : expected HTTP $($Expected -join ' or '), got $($result.Status). $detail"
    }

    return $result
}

function Wait-ForReady {
    param(
        [string] $Uri,
        [int] $TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $attempt = 0

    while ((Get-Date) -lt $deadline) {
        $attempt++
        $result = Invoke-HttpProbe -Method 'Get' -Uri $Uri
        if ($result.Status -eq 200) {
            Write-Pass "readiness healthy after $attempt attempt(s)"
            return
        }

        Write-Host "  ....  attempt $attempt : HTTP $($result.Status); retrying in 5s"
        Start-Sleep -Seconds 5
    }

    Write-Fail "readiness never reported healthy within $TimeoutSeconds seconds ($Uri)"
}

function Get-AccessToken {
    param([string] $Resource)

    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        throw 'Azure CLI (az) is not on PATH. Install it or pass -BearerToken.'
    }

    $output = (az account get-access-token --resource $Resource --query accessToken -o tsv 2>&1 |
        ForEach-Object { $_.ToString() }) -join '; '

    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($output)) {
        throw "Could not acquire a token for $Resource. Run scripts/setup-entra-app.ps1 first. Azure CLI said: $output"
    }

    return $output.Trim()
}

function Get-PreviewBody {
    param([string] $Code)

    return (@{
            code                = $Code
            customerId          = 'e2e-test-customer'
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
        } | ConvertTo-Json -Depth 6 -Compress)
}

function Get-OrderBody {
    param([string] $Code)

  # Catalog prices: margherita 9.50 x2 + bbq-chicken 12.00 = 31.00; SAVE10 => 3.10 off.
    return (@{
            customerId   = 'e2e-test-customer'
            couponCode   = $Code
            clientTotal  = 1.00
            lines        = @(
                @{ pizzaId = 'margherita'; quantity = 2 }
                @{ pizzaId = 'bbq-chicken'; quantity = 1 }
            )
        } | ConvertTo-Json -Depth 6 -Compress)
}

$gateway = Get-NormalizedGateway -Value $GatewayUrl
$couponBase = "$gateway/coupons"
$orderBase = "$gateway/orders"

Write-Host "End-to-end test"
Write-Host "  Gateway : $gateway"
Write-Host "  Coupons : $couponBase"
Write-Host "  Orders  : $orderBase"
Write-Host "  Coupon  : $CouponCode"

Write-Step '1. Health (anonymous)'
$null = Assert-HttpStatus -Name 'GET /coupons/v1/health/live' `
    -Method 'Get' -Uri "$couponBase/v1/health/live" -Expected @(200)
Wait-ForReady -Uri "$couponBase/v1/health/ready" -TimeoutSeconds $ReadyTimeoutSeconds

Write-Step '2. Gateway auth (expect 401 without token)'
$previewBody = Get-PreviewBody -Code $CouponCode
$null = Assert-HttpStatus -Name 'POST preview without token' `
    -Method 'Post' -Uri "$couponBase/v1/coupons/preview" -Expected @(401) -Body $previewBody
$null = Assert-HttpStatus -Name 'GET pizzas without token' `
    -Method 'Get' -Uri "$orderBase/v1/pizzas" -Expected @(401)

Write-Step '3. Acquire bearer token'
if ([string]::IsNullOrWhiteSpace($BearerToken)) {
    $BearerToken = Get-AccessToken -Resource $Audience
    Write-Host "  ....  token acquired for $Audience"
}
else {
    Write-Host '  ....  using supplied -BearerToken'
}

Write-Step '4. Coupon preview'
$preview = Assert-HttpStatus -Name 'POST preview with token' `
    -Method 'Post' -Uri "$couponBase/v1/coupons/preview" -Expected @(200) `
    -Body $previewBody -Token $BearerToken

if ($preview.Status -eq 200) {
    $status = [string] $preview.Body.status
    if ($status -eq 'Applied') {
        Write-Pass "preview status is Applied"
    }
    else {
        Write-Fail "preview status expected Applied, got '$status'"
    }

    if ($CouponCode -eq 'SAVE10') {
        $expected = @{ subtotal = [decimal] 40.00; discount = [decimal] 4.00; total = [decimal] 36.00 }
        foreach ($field in @('subtotal', 'discount', 'total')) {
            $actual = [decimal] $preview.Body.pricing.$field
            if ($actual -eq $expected[$field]) {
                Write-Pass "preview $field is $actual"
            }
            else {
                Write-Fail "preview $field expected $($expected[$field]), got $actual"
            }
        }
    }
    else {
        Write-Host "  ....  skipped fixed SAVE10 money assertions (CouponCode=$CouponCode)"
        Write-Host "       pricing: $($preview.Body.pricing | ConvertTo-Json -Compress)"
    }
}

Write-Step '5. Pizza catalog'
$catalog = Assert-HttpStatus -Name 'GET /orders/v1/pizzas' `
    -Method 'Get' -Uri "$orderBase/v1/pizzas" -Expected @(200) -Token $BearerToken

if ($catalog.Status -eq 200) {
    $count = @($catalog.Body.pizzas).Count
    if ($count -gt 0) {
        Write-Pass "catalog returned $count pizza(s), currency $($catalog.Body.currency)"
    }
    else {
        Write-Fail 'catalog returned no pizzas'
    }
}

if (-not $SkipOrder) {
    Write-Step '6. Place order (checkout)'
    $orderBody = Get-OrderBody -Code $CouponCode
    $created = Assert-HttpStatus -Name 'POST /orders/v1/orders' `
        -Method 'Post' -Uri "$orderBase/v1/orders" -Expected @(200, 201) `
        -Body $orderBody -Token $BearerToken

    if ($created.Status -in @(200, 201)) {
        $orderId = [string] $created.Body.orderId
        if ([string]::IsNullOrWhiteSpace($orderId)) {
            Write-Fail 'order response missing orderId'
        }
        else {
            Write-Pass "order created: $orderId"
            Write-Host "       total $($created.Body.total) $($created.Body.currency) (server-priced)"

            if ($CouponCode -eq 'SAVE10') {
                $sub = [decimal] $created.Body.subtotal
                $disc = [decimal] $created.Body.discount
                $tot = [decimal] $created.Body.total
                if ($sub -eq 31.00m -and $disc -eq 3.10m -and $tot -eq 27.90m) {
                    Write-Pass 'order SAVE10 pricing (31.00 - 3.10 = 27.90)'
                }
                else {
                    Write-Fail "order SAVE10 pricing expected 31/3.10/27.90, got $sub/$disc/$tot"
                }
            }

            Write-Step '7. Fetch order'
            $fetched = Assert-HttpStatus -Name "GET /orders/v1/orders/$orderId" `
                -Method 'Get' -Uri "$orderBase/v1/orders/$orderId" -Expected @(200) -Token $BearerToken

            if ($fetched.Status -eq 200 -and [string] $fetched.Body.orderId -eq $orderId) {
                Write-Pass 'GET order matches created id'
            }
            elseif ($fetched.Status -eq 200) {
                Write-Fail 'GET order id mismatch'
            }
        }
    }
}
else {
    Write-Host ''
    Write-Host 'Skipped order placement (-SkipOrder).'
}

Write-Host ''
Write-Host "Results: $($script:Passes) passed, $($script:Failures.Count) failed."

if ($script:Failures.Count -gt 0) {
    Write-Host ''
    Write-Host 'Failures:'
    foreach ($failure in $script:Failures) {
        Write-Host "  - $failure"
    }

    throw "End-to-end test failed: $($script:Failures.Count) check(s) did not pass."
}

Write-Host ''
Write-Host 'End-to-end test passed.' -ForegroundColor Green
