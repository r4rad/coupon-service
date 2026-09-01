<#
.SYNOPSIS
    Creates or converges the Coupon Service API app registration, its app roles, and the
    app-role assignments that CD Seed and the Order API hop depend on.

.DESCRIPTION
    One-time operator prerequisite from docs/pipeline-prerequisites.md section 4, expressed as
    code so it can be re-run and reviewed. Idempotent: every step reads current Graph state
    first and only writes the difference.

    Creates:
      1. Application `Coupon Service API` with identifier URI $Audience.
      2. App roles `Coupon.Admin` and `Coupon.Redeem` (existing role ids are preserved, so
         assignments already granted keep working).
      3. Delegated scope `access_as_user` with Microsoft Azure CLI pre-authorized so
         `az account get-access-token --resource api://coupon-service` works for manual testing.
      4. The application's service principal, which is the resource side of an assignment.
      5. `Coupon.Admin` on the pipeline WIF principal, so CD Seed can obtain a real Entra JWT
         instead of relying on the expiring AdminApiBearerToken fallback (AC-9.5, AC-9.6).
      6. Optionally `Coupon.Redeem` on the Order API managed identity (AC-7.7).

    Requires an `az login` session with rights to create app registrations and write
    appRoleAssignedTo in the target tenant.

.PARAMETER Audience
    Application ID URI, and the value Bicep passes as `couponApiAudience`. Some tenants
    restrict identifier URIs to `api://{appId}`; if creation fails on the URI, re-run with
    `-Audience api://<appId>` and override `couponApiAudience` in the bicepparam files.

.PARAMETER AdminPrincipalId
    Object id (not app id) of the service principal that CD runs as, taken from the
    workload-identity service connection. Receives `Coupon.Admin`.

.PARAMETER RedeemPrincipalId
    Object id of the Order API user-assigned managed identity. Receives `Coupon.Redeem`.
    Omit until Bicep has created `id-order-api-{env}`.

.EXAMPLE
    ./scripts/setup-entra-app.ps1 -AdminPrincipalId 24fa000f-3b0a-4ae1-84f2-70e803aca6e0 -DryRun
    ./scripts/setup-entra-app.ps1 -AdminPrincipalId 24fa000f-3b0a-4ae1-84f2-70e803aca6e0
#>
[CmdletBinding()]
param(
    [string] $DisplayName = 'Coupon Service API',
    [string] $Audience = 'api://coupon-service',
    [string] $AdminPrincipalId,
    [string] $RedeemPrincipalId,
    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'

$graph = 'https://graph.microsoft.com/v1.0'

# Microsoft Azure CLI — pre-authorized so operators can run
# az account get-access-token --resource api://coupon-service without AADSTS65001.
$AzureCliAppId = '04b07795-8ddb-461a-bbee-02f9e1bf7b46'
# Stable scope id: changing it invalidates existing consent and pre-auth links.
$DelegatedScopeId = '8c29f0b1-5e4a-4d2c-9a1b-7e3c5d8f2a10'
$DelegatedScopeValue = 'access_as_user'

function Write-Step { param([string]$Message) Write-Host "`n=== $Message" -ForegroundColor Cyan }
function Write-Info { param([string]$Message) Write-Host "    $Message" -ForegroundColor DarkGray }

# PowerShell 5.1 surfaces native-command stderr as an ErrorRecord, which terminates under
# ErrorActionPreference = Stop. Treat az output as data plus an exit code, which is what it is.
function Invoke-Native {
    param([string] $Exe, [string[]] $Arguments)
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $raw = & $Exe @Arguments 2>&1
        return [pscustomobject]@{
            Output = (($raw | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine)
            Code   = $LASTEXITCODE
        }
    }
    finally { $ErrorActionPreference = $previous }
}

function Invoke-Graph {
    param(
        [ValidateSet('get', 'post', 'patch')]
        [string]    $Method,
        [string]    $Url,
        [hashtable] $Body,
        [switch]    $AllowNotFound
    )

    $arguments = @('rest', '--method', $Method, '--url', $Url)
    $bodyFile = $null
    if ($null -ne $Body) {
        $json = $Body | ConvertTo-Json -Depth 10 -Compress
        # Pass the body by file: PowerShell 5.1 quoting mangles inline JSON, and Set-Content
        # would prepend a BOM that Graph rejects as malformed JSON.
        $bodyFile = [IO.Path]::GetTempFileName()
        [IO.File]::WriteAllText($bodyFile, $json, (New-Object System.Text.UTF8Encoding($false)))
        $arguments += @('--headers', 'Content-Type=application/json', '--body', "@$bodyFile")
    }

    try {
        $result = Invoke-Native -Exe 'az' -Arguments $arguments
    }
    finally {
        if ($bodyFile -and (Test-Path -LiteralPath $bodyFile)) {
            Remove-Item -LiteralPath $bodyFile -Force
        }
    }

    if ($result.Code -ne 0) {
        if ($AllowNotFound -and $result.Output -match 'ResourceNotFound|Request_ResourceNotFound|\b404\b') {
            return $null
        }
        throw "Graph $Method $Url failed (exit $($result.Code)): $($result.Output)"
    }

    if ([string]::IsNullOrWhiteSpace($result.Output)) { return $null }
    return $result.Output | ConvertFrom-Json
}

function Get-DesiredAppRoles {
    param($ExistingRoles)

    $desired = @(
        [pscustomobject]@{
            Value             = 'Coupon.Admin'
            DisplayName       = 'Coupon administration'
            Description       = 'Create, update and retire coupon policies.'
            AllowedMemberTypes = @('Application', 'User')
        },
        [pscustomobject]@{
            Value             = 'Coupon.Redeem'
            DisplayName       = 'Coupon redemption'
            Description       = 'Reserve, confirm and release coupon redemptions.'
            AllowedMemberTypes = @('Application')
        }
    )

    $merged = @()
    foreach ($role in $desired) {
        $existing = $ExistingRoles | Where-Object { $_.value -eq $role.Value } | Select-Object -First 1
        # Reuse the existing id: changing it silently invalidates every appRoleAssignment
        # already granted against this role.
        $roleId = if ($existing) { [string]$existing.id } else { [guid]::NewGuid().ToString() }
        $merged += @{
            id                 = $roleId
            value              = $role.Value
            displayName        = $role.DisplayName
            description        = $role.Description
            allowedMemberTypes = $role.AllowedMemberTypes
            isEnabled          = $true
        }
    }

    # Keep roles this script does not own rather than deleting them.
    foreach ($existing in $ExistingRoles) {
        if ($desired.Value -notcontains $existing.value) {
            $merged += @{
                id                 = [string]$existing.id
                value              = [string]$existing.value
                displayName        = [string]$existing.displayName
                description        = [string]$existing.description
                allowedMemberTypes = @($existing.allowedMemberTypes)
                isEnabled          = [bool]$existing.isEnabled
            }
        }
    }

    return $merged
}

function Get-DesiredApiConfiguration {
    param($ExistingApi)

    $existingScopes = @()
    if ($null -ne $ExistingApi -and $null -ne $ExistingApi.oauth2PermissionScopes) {
        $existingScopes = @($ExistingApi.oauth2PermissionScopes)
    }

    $scope = $existingScopes | Where-Object { $_.value -eq $DelegatedScopeValue } | Select-Object -First 1
    $scopeId = if ($scope) { [string]$scope.id } else { $DelegatedScopeId }

    $desiredScope = @{
        adminConsentDescription = 'Allow the application to access the Coupon Service API on behalf of the signed-in user.'
        adminConsentDisplayName = 'Access Coupon Service API'
        id                      = $scopeId
        isEnabled               = $true
        type                    = 'User'
        userConsentDescription  = 'Allow the application to access the Coupon Service API on your behalf.'
        userConsentDisplayName  = 'Access Coupon Service API'
        value                   = $DelegatedScopeValue
    }

    $mergedScopes = @($desiredScope)
    foreach ($existing in $existingScopes) {
        if ([string]$existing.value -ne $DelegatedScopeValue) {
            $mergedScopes += @{
                adminConsentDescription = [string]$existing.adminConsentDescription
                adminConsentDisplayName = [string]$existing.adminConsentDisplayName
                id                      = [string]$existing.id
                isEnabled               = [bool]$existing.isEnabled
                type                    = [string]$existing.type
                userConsentDescription  = [string]$existing.userConsentDescription
                userConsentDisplayName  = [string]$existing.userConsentDisplayName
                value                   = [string]$existing.value
            }
        }
    }

    $existingPreAuth = @()
    if ($null -ne $ExistingApi -and $null -ne $ExistingApi.preAuthorizedApplications) {
        $existingPreAuth = @($ExistingApi.preAuthorizedApplications)
    }

    $cliPreAuth = $existingPreAuth | Where-Object { $_.appId -eq $AzureCliAppId } | Select-Object -First 1
    $mergedPreAuth = @()
    if ($cliPreAuth) {
        $permissionIds = @($cliPreAuth.delegatedPermissionIds | ForEach-Object { [string]$_ })
        if ($permissionIds -notcontains $scopeId) {
            $permissionIds += $scopeId
        }

        $mergedPreAuth += @{
            appId                   = $AzureCliAppId
            delegatedPermissionIds  = $permissionIds
        }
    }
    else {
        $mergedPreAuth += @{
            appId                   = $AzureCliAppId
            delegatedPermissionIds  = @($scopeId)
        }
    }

    foreach ($existing in $existingPreAuth) {
        if ([string]$existing.appId -ne $AzureCliAppId) {
            $mergedPreAuth += @{
                appId                   = [string]$existing.appId
                delegatedPermissionIds  = @($existing.delegatedPermissionIds | ForEach-Object { [string]$_ })
            }
        }
    }

    return @{
        requestedAccessTokenVersion = 2
        oauth2PermissionScopes      = $mergedScopes
        preAuthorizedApplications   = $mergedPreAuth
    }
}

function Test-ApiConfigurationConverged {
    param($ExistingApi)

    if ($null -eq $ExistingApi) { return $false }
    if ([int]$ExistingApi.requestedAccessTokenVersion -ne 2) { return $false }

    $scope = @($ExistingApi.oauth2PermissionScopes) |
        Where-Object { $_.value -eq $DelegatedScopeValue -and $_.isEnabled } |
        Select-Object -First 1
    if ($null -eq $scope) { return $false }

    $cliPreAuth = @($ExistingApi.preAuthorizedApplications) |
        Where-Object { $_.appId -eq $AzureCliAppId } |
        Select-Object -First 1
    if ($null -eq $cliPreAuth) { return $false }

    return @($cliPreAuth.delegatedPermissionIds | ForEach-Object { [string]$_ }) -contains [string]$scope.id
}

function Set-ApiConfiguration {
    param(
        [string] $ApplicationObjectId,
        $ExistingApi
    )

    if (Test-ApiConfigurationConverged -ExistingApi $ExistingApi) {
        return Invoke-Graph -Method get -Url "$graph/applications/$ApplicationObjectId"
    }

    $desiredApi = Get-DesiredApiConfiguration -ExistingApi $ExistingApi
    $scope = @($desiredApi.oauth2PermissionScopes) |
        Where-Object { $_.value -eq $DelegatedScopeValue } |
        Select-Object -First 1

    if ($DryRun) {
        Write-Info "[dry run] would patch api scopes and Azure CLI pre-authorization."
        return $ExistingApi
    }

  # Graph rejects preAuthorizedApplications in the same write when the scope id is new.
    $scopePatch = @{
        api = @{
            requestedAccessTokenVersion = 2
            oauth2PermissionScopes      = $desiredApi.oauth2PermissionScopes
        }
    }
    Invoke-Graph -Method patch -Url "$graph/applications/$ApplicationObjectId" -Body $scopePatch | Out-Null

    $refreshed = Invoke-Graph -Method get -Url "$graph/applications/$ApplicationObjectId"
    $scopeId = @($refreshed.api.oauth2PermissionScopes) |
        Where-Object { $_.value -eq $DelegatedScopeValue } |
        Select-Object -First 1 -ExpandProperty id

    $preAuthPatch = @{
        api = @{
            preAuthorizedApplications = @(
                @{
                    appId                  = $AzureCliAppId
                    delegatedPermissionIds = @([string]$scopeId)
                }
            )
        }
    }

    $existingOther = @($refreshed.api.preAuthorizedApplications) |
        Where-Object { $_.appId -ne $AzureCliAppId }
    foreach ($existing in $existingOther) {
        $preAuthPatch.api.preAuthorizedApplications += @{
            appId                  = [string]$existing.appId
            delegatedPermissionIds = @($existing.delegatedPermissionIds | ForEach-Object { [string]$_ })
        }
    }

    Invoke-Graph -Method patch -Url "$graph/applications/$ApplicationObjectId" -Body $preAuthPatch | Out-Null
    return Invoke-Graph -Method get -Url "$graph/applications/$ApplicationObjectId"
}

function Grant-AzureCliDelegatedAccess {
    param(
        [string] $ResourceSpId
    )

    $clientFilter = [uri]::EscapeDataString("appId eq '$AzureCliAppId'")
    $clientSp = Invoke-Graph -Method get -Url "$graph/servicePrincipals?`$filter=$clientFilter"
    $clientSpId = [string]($clientSp.value | Select-Object -First 1 -ExpandProperty id)
    if ([string]::IsNullOrWhiteSpace($clientSpId)) {
        if ($DryRun) {
            Write-Info "[dry run] would create the Microsoft Azure CLI service principal."
            return
        }

        $created = Invoke-Graph -Method post -Url "$graph/servicePrincipals" -Body @{ appId = $AzureCliAppId }
        $clientSpId = [string]$created.id
        Write-Info "Created Microsoft Azure CLI service principal $clientSpId."
    }

    $grantFilter = [uri]::EscapeDataString(
        "clientId eq '$clientSpId' and resourceId eq '$ResourceSpId' and consentType eq 'AllPrincipals'")
    $existing = Invoke-Graph -Method get -Url "$graph/oauth2PermissionGrants?`$filter=$grantFilter"
    $grant = $existing.value | Select-Object -First 1
    if ($grant -and [string]$grant.scope -match $DelegatedScopeValue) {
        Write-Info "Tenant-wide Azure CLI consent for $DelegatedScopeValue already granted."
        return
    }

    if ($DryRun) {
        Write-Info "[dry run] would grant tenant-wide Azure CLI consent for $DelegatedScopeValue."
        return
    }

    Invoke-Graph -Method post -Url "$graph/oauth2PermissionGrants" -Body @{
        clientId    = $clientSpId
        consentType = 'AllPrincipals'
        principalId = $null
        resourceId  = $ResourceSpId
        scope       = $DelegatedScopeValue
    } | Out-Null
    Write-Info "Granted tenant-wide Azure CLI consent for $DelegatedScopeValue."
}

function Grant-AppRole {
    param(
        [string] $ResourceSpId,
        [string] $PrincipalId,
        [string] $RoleId,
        [string] $RoleValue
    )

    $assignments = Invoke-Graph -Method get -Url "$graph/servicePrincipals/$ResourceSpId/appRoleAssignedTo"
    $already = $assignments.value | Where-Object {
        $_.principalId -eq $PrincipalId -and $_.appRoleId -eq $RoleId
    }
    if ($already) {
        Write-Info "$RoleValue already assigned to $PrincipalId."
        return
    }

    if ($DryRun) {
        Write-Info "[dry run] would assign $RoleValue to $PrincipalId."
        return
    }

    Invoke-Graph -Method post -Url "$graph/servicePrincipals/$ResourceSpId/appRoleAssignedTo" -Body @{
        principalId = $PrincipalId
        resourceId  = $ResourceSpId
        appRoleId   = $RoleId
    } | Out-Null
    Write-Info "Assigned $RoleValue to $PrincipalId."
}

$account = Invoke-Graph -Method get -Url "$graph/organization?`$select=id,displayName"
Write-Step "Tenant $($account.value[0].displayName) ($($account.value[0].id))"
if ($DryRun) { Write-Info 'Dry run: no writes will be made.' }

Write-Step "Application $DisplayName ($Audience)"

$filter = [uri]::EscapeDataString("identifierUris/any(u:u eq '$Audience')")
$found = Invoke-Graph -Method get -Url "$graph/applications?`$filter=$filter"
$app = $found.value | Select-Object -First 1

if ($null -eq $app) {
    $appRoles = Get-DesiredAppRoles -ExistingRoles @()
    if ($DryRun) {
        Write-Info "[dry run] would create application with roles: $(($appRoles | ForEach-Object { $_.value }) -join ', ')."
        return
    }

    $initialApi = Get-DesiredApiConfiguration -ExistingApi $null
    $app = Invoke-Graph -Method post -Url "$graph/applications" -Body @{
        displayName     = $DisplayName
        signInAudience  = 'AzureADMyOrg'
        identifierUris  = @($Audience)
        appRoles        = $appRoles
        api             = @{
            requestedAccessTokenVersion = 2
            oauth2PermissionScopes      = $initialApi.oauth2PermissionScopes
        }
    }
    Write-Info "Created application $($app.appId) (object $($app.id))."
    $app = Set-ApiConfiguration -ApplicationObjectId $app.id -ExistingApi $app.api
}
else {
    Write-Info "Found application $($app.appId) (object $($app.id))."

    $patch = @{}
    if (-not (Test-ApiConfigurationConverged -ExistingApi $app.api)) {
        Write-Info "Delegated scope '$DelegatedScopeValue' and/or Azure CLI pre-auth missing; will patch."
        if (-not $DryRun) {
            $app = Set-ApiConfiguration -ApplicationObjectId $app.id -ExistingApi $app.api
            Write-Info 'API scopes and Azure CLI pre-authorization patched.'
        }
    }

    $appRoles = Get-DesiredAppRoles -ExistingRoles @($app.appRoles)
    $existingValues = @($app.appRoles | ForEach-Object { $_.value })
    if (@($appRoles | Where-Object { $existingValues -notcontains $_.value }).Count -gt 0) {
        $patch['appRoles'] = $appRoles
        Write-Info 'App roles missing; will patch.'
    }

    if ($patch.Count -eq 0) {
        Write-Info 'Application already converged.'
    }
    elseif ($DryRun) {
        Write-Info "[dry run] would patch: $(($patch.Keys) -join ', ')."
    }
    else {
        Invoke-Graph -Method patch -Url "$graph/applications/$($app.id)" -Body $patch | Out-Null
        $app = Invoke-Graph -Method get -Url "$graph/applications/$($app.id)"
        Write-Info 'Application patched.'
    }
}

Write-Step 'Service principal for the application'

$spFilter = [uri]::EscapeDataString("appId eq '$($app.appId)'")
$spFound = Invoke-Graph -Method get -Url "$graph/servicePrincipals?`$filter=$spFilter"
$sp = $spFound.value | Select-Object -First 1

if ($null -eq $sp) {
    if ($DryRun) {
        Write-Info '[dry run] would create the service principal.'
        return
    }
    # Without the resource service principal there is nothing to assign app roles against,
    # and az account get-access-token --resource returns AADSTS500011.
    $sp = Invoke-Graph -Method post -Url "$graph/servicePrincipals" -Body @{ appId = $app.appId }
    Write-Info "Created service principal $($sp.id)."
}
else {
    Write-Info "Found service principal $($sp.id)."
}

Grant-AzureCliDelegatedAccess -ResourceSpId $sp.id

Write-Step 'App role assignments'

$roleIds = @{}
foreach ($role in @($app.appRoles)) { $roleIds[[string]$role.value] = [string]$role.id }

if ($AdminPrincipalId) {
    Grant-AppRole -ResourceSpId $sp.id -PrincipalId $AdminPrincipalId -RoleId $roleIds['Coupon.Admin'] -RoleValue 'Coupon.Admin'
}
else {
    Write-Info 'AdminPrincipalId not supplied; CD Seed will still fail with 401 until Coupon.Admin is assigned.'
}

if ($RedeemPrincipalId) {
    Grant-AppRole -ResourceSpId $sp.id -PrincipalId $RedeemPrincipalId -RoleId $roleIds['Coupon.Redeem'] -RoleValue 'Coupon.Redeem'
}
else {
    Write-Info 'RedeemPrincipalId not supplied; skipping Coupon.Redeem (assign after Bicep creates the Order API identity).'
}

Write-Step 'Summary'
Write-Host "  couponApiAudience : $Audience"
Write-Host "  couponApiClientId : $($app.appId)"
Write-Host "  resource sp objId : $($sp.id)"
Write-Host "  Coupon.Admin  role: $($roleIds['Coupon.Admin'])"
Write-Host "  Coupon.Redeem role: $($roleIds['Coupon.Redeem'])"
Write-Host ''
# Version 2 tokens carry the client id in aud, never the Application ID URI, so the templates
# need both values. A stale couponApiClientId presents as a 401 with a valid-looking token.
Write-Host "  Set 'param couponApiClientId' to $($app.appId) in infra/bicep/main.*.bicepparam."
Write-Host "  Verify with: az account get-access-token --resource $Audience"
Write-Host "  (Do not use az login --scope; login normally, then run get-access-token with --resource.)"
