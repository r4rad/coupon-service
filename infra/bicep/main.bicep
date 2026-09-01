targetScope = 'resourceGroup'

@description('Azure region. Defaults to westeurope for the demo.')
param location string = 'westeurope'

@description('Static Web Apps Free SKU region. May differ from location when SWA is unavailable there.')
param staticWebAppLocation string = 'westeurope'

@description('Container Apps environment region. Empty inherits location. Override only when the subscription quota allows a dedicated second CAE in another region (CS-29).')
param containerAppsLocation string = ''

@description('Resource group of an existing Container Apps environment to reuse instead of creating one. Set with existingManagedEnvironmentName when the subscription global one-CAE quota is exhausted.')
param existingManagedEnvironmentResourceGroup string = ''

@description('Name of the existing Container Apps environment to reuse. Pair with existingManagedEnvironmentResourceGroup when prod shares the non-prod CAE.')
param existingManagedEnvironmentName string = ''

@description('Short environment label applied to names and the env tag.')
param environmentName string = 'demo'

@description('Project tag value for cost filtering.')
param projectName string = 'coupon-service'

@description('Owner tag value so the demo environment can be deleted in one sweep.')
param ownerTag string = 'coupon-demo'

@description('Hosting path: containerApps (recommended) or appService (strict zero-cost F1 fallback).')
@allowed([
  'containerApps'
  'appService'
])
param hostingMode string = 'containerApps'

@description('Enable Cosmos free tier when the subscription still has the allotment.')
param cosmosEnableFreeTier bool = true

@description('Log Analytics daily ingest cap in GB.')
param logAnalyticsDailyCapGb int = 1

@description('Public placeholder image for first Container Apps provision (P-11).')
param placeholderImage string = 'mcr.microsoft.com/k8se/quickstart:latest'

@description('APIM publisher email. Not a secret.')
param apimPublisherEmail string = 'noreply@example.com'

@description('APIM publisher display name.')
param apimPublisherName string = 'Coupon Demo'

@description('Entra tenant id for JWT validation. Defaults to the deployment tenant so APIM openid-config resolves.')
param entraTenantId string = tenant().tenantId

@description('Coupon Service API Application ID URI (JWT audience and MI token resource).')
param couponApiAudience string = 'api://coupon-service'

@description('Coupon Service app registration client id. Version 2 tokens carry this GUID in aud instead of the Application ID URI, so it must be a valid audience. Emitted by scripts/setup-entra-app.ps1.')
param couponApiClientId string = ''

@description('Seeds the deterministic policy set as the Coupon Service starts (AC-9.5, AC-9.6). The policy store is per-instance, so seeding belongs with the instance rather than in a pipeline step.')
param seedPoliciesOnStartup bool = true

@description('Expose Scalar, ReDoc, and /openapi on API hosts. True for develop CD only; production (main) leaves this false.')
param enableApiDocumentation bool = false

@description('Allowed SPA origin for APIM CORS on the customer product.')
param spaOrigin string = 'https://localhost:5173'

// Derived from the deployment resource group so the template never bakes in an RG name (AC-9.1).
// Leading salt: Key Vault uses take(..., 24), which drops a trailing salt and kept colliding on
// soft-deleted kv-coupon-demo-r4hxkv774. Prefix must change the truncated name (CS-29).
var uniqueSuffix = take('v29${uniqueString(resourceGroup().id)}', 13)

var tags = {
  project: projectName
  env: environmentName
  owner: ownerTag
}

module observability 'modules/observability.bicep' = {
  name: 'observability'
  params: {
    location: location
    environmentName: environmentName
    dailyCapGb: logAnalyticsDailyCapGb
    tags: tags
  }
}

module identity 'modules/identity.bicep' = {
  name: 'identity'
  params: {
    location: location
    environmentName: environmentName
    tags: tags
  }
}

module keyvault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    location: location
    environmentName: environmentName
    uniqueSuffix: uniqueSuffix
    secretsUserPrincipalIds: [
      identity.outputs.couponIdentityPrincipalId
      identity.outputs.orderIdentityPrincipalId
    ]
    tags: tags
  }
}

module cosmos 'modules/cosmos.bicep' = {
  name: 'cosmos'
  params: {
    location: location
    environmentName: environmentName
    uniqueSuffix: uniqueSuffix
    enableFreeTier: cosmosEnableFreeTier
    tags: tags
  }
}

module acr 'modules/acr.bicep' = {
  name: 'acr'
  params: {
    location: location
    uniqueSuffix: uniqueSuffix
    pullPrincipalIds: [
      identity.outputs.couponIdentityPrincipalId
      identity.outputs.orderIdentityPrincipalId
    ]
    tags: tags
  }
}

// loginEndpoint includes a trailing slash (no-hardcoded-env-urls).
var jwtAuthority = '${environment().authentication.loginEndpoint}${entraTenantId}/v2.0'
var jwtIssuer = jwtAuthority
var openIdConfigUrl = '${jwtAuthority}/.well-known/openid-configuration'
var couponServiceScope = '${couponApiAudience}/.default'

// APIM policy XML cannot conditionally omit an <audience>, so fall back to the Application ID
// URI when the client id is unknown. That yields a duplicate audience rather than an empty one.
var couponApiClientIdOrAudience = empty(couponApiClientId) ? couponApiAudience : couponApiClientId

var reuseManagedEnvironment = !empty(existingManagedEnvironmentResourceGroup) && !empty(existingManagedEnvironmentName)
var existingManagedEnvironmentResourceId = reuseManagedEnvironment
  ? resourceId(existingManagedEnvironmentResourceGroup, 'Microsoft.App/managedEnvironments', existingManagedEnvironmentName)
  : ''

var containerAppsRegion = containerAppsLocation == '' ? location : containerAppsLocation

module containerapps 'modules/containerapps.bicep' = if (hostingMode == 'containerApps') {
  name: 'containerapps'
  params: {
    location: containerAppsRegion
    environmentName: environmentName
    existingManagedEnvironmentResourceId: existingManagedEnvironmentResourceId
    logAnalyticsWorkspaceName: observability.outputs.logAnalyticsName
    appInsightsConnectionString: observability.outputs.appInsightsConnectionString
    couponIdentityId: identity.outputs.couponIdentityId
    couponIdentityClientId: identity.outputs.couponIdentityClientId
    orderIdentityId: identity.outputs.orderIdentityId
    orderIdentityClientId: identity.outputs.orderIdentityClientId
    placeholderImage: placeholderImage
    acrLoginServer: acr.outputs.acrLoginServer
    jwtAuthority: jwtAuthority
    couponApiAudience: couponApiAudience
    couponApiClientId: couponApiClientId
    couponServiceScope: couponServiceScope
    seedPoliciesOnStartup: seedPoliciesOnStartup
    enableApiDocumentation: enableApiDocumentation
    tags: tags
  }
}

module appservice 'modules/appservice.bicep' = if (hostingMode == 'appService') {
  name: 'appservice'
  params: {
    location: location
    environmentName: environmentName
    uniqueSuffix: uniqueSuffix
    appInsightsConnectionString: observability.outputs.appInsightsConnectionString
    couponIdentityId: identity.outputs.couponIdentityId
    couponIdentityClientId: identity.outputs.couponIdentityClientId
    orderIdentityId: identity.outputs.orderIdentityId
    orderIdentityClientId: identity.outputs.orderIdentityClientId
    jwtAuthority: jwtAuthority
    couponApiAudience: couponApiAudience
    couponApiClientId: couponApiClientId
    couponServiceScope: couponServiceScope
    seedPoliciesOnStartup: seedPoliciesOnStartup
    enableApiDocumentation: enableApiDocumentation
    tags: tags
  }
}

module apim 'modules/apim.bicep' = {
  name: 'apim'
  params: {
    location: location
    environmentName: environmentName
    uniqueSuffix: uniqueSuffix
    publisherEmail: apimPublisherEmail
    publisherName: apimPublisherName
    tags: tags
  }
}

var couponBackendUrl = hostingMode == 'containerApps'
  ? 'https://${containerapps!.outputs.couponAppFqdn}'
  : 'https://${appservice!.outputs.couponAppHostName}'

var orderBackendUrl = hostingMode == 'containerApps'
  ? 'https://${containerapps!.outputs.orderAppFqdn}'
  : 'https://${appservice!.outputs.orderAppHostName}'

module apimApi 'modules/apim-api.bicep' = {
  name: 'apim-api'
  params: {
    apimName: apim.outputs.apimName
    couponBackendUrl: couponBackendUrl
    orderBackendUrl: orderBackendUrl
    entraTenantId: entraTenantId
    couponApiAudience: couponApiAudience
    couponApiClientId: couponApiClientIdOrAudience
    spaOrigin: spaOrigin
    openIdConfigUrl: openIdConfigUrl
    jwtIssuer: jwtIssuer
  }
}

module staticwebapp 'modules/staticwebapp.bicep' = {
  name: 'staticwebapp'
  params: {
    location: staticWebAppLocation
    environmentName: environmentName
    tags: tags
  }
}

output hostingMode string = hostingMode
output environmentName string = environmentName
output apimGatewayUrl string = apim.outputs.apimGatewayUrl
output cosmosEndpoint string = cosmos.outputs.cosmosEndpoint
output keyVaultUri string = keyvault.outputs.keyVaultUri
output acrLoginServer string = acr.outputs.acrLoginServer
output acrName string = acr.outputs.acrName
output staticWebAppHostname string = staticwebapp.outputs.staticWebAppDefaultHostname
output couponBackendUrl string = couponBackendUrl
output orderBackendUrl string = orderBackendUrl
output couponAppName string = hostingMode == 'containerApps' ? containerapps!.outputs.couponAppName : appservice!.outputs.couponAppName
output orderAppName string = hostingMode == 'containerApps' ? containerapps!.outputs.orderAppName : appservice!.outputs.orderAppName
output appInsightsConnectionString string = observability.outputs.appInsightsConnectionString
output orderIdentityClientId string = identity.outputs.orderIdentityClientId
output couponIdentityClientId string = identity.outputs.couponIdentityClientId
output couponApiAudience string = couponApiAudience
output entraTenantId string = entraTenantId
