targetScope = 'resourceGroup'

@description('Azure region. Defaults to westeurope for the demo.')
param location string = 'westeurope'

@description('Static Web Apps Free SKU region. May differ from location when SWA is unavailable there.')
param staticWebAppLocation string = 'westeurope'

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

// Derived from the deployment resource group so the template never bakes in an RG name (AC-9.1).
// Trailing salt avoids global name collisions with soft-deleted APIM from earlier failed applies.
var uniqueSuffix = '${uniqueString(resourceGroup().id)}cs27'

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
    tags: tags
  }
}

module containerapps 'modules/containerapps.bicep' = if (hostingMode == 'containerApps') {
  name: 'containerapps'
  params: {
    location: location
    environmentName: environmentName
    logAnalyticsWorkspaceName: observability.outputs.logAnalyticsName
    appInsightsConnectionString: observability.outputs.appInsightsConnectionString
    couponIdentityId: identity.outputs.couponIdentityId
    couponIdentityClientId: identity.outputs.couponIdentityClientId
    orderIdentityId: identity.outputs.orderIdentityId
    orderIdentityClientId: identity.outputs.orderIdentityClientId
    placeholderImage: placeholderImage
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
output apimGatewayUrl string = apim.outputs.apimGatewayUrl
output cosmosEndpoint string = cosmos.outputs.cosmosEndpoint
output keyVaultUri string = keyvault.outputs.keyVaultUri
output acrLoginServer string = acr.outputs.acrLoginServer
output staticWebAppHostname string = staticwebapp.outputs.staticWebAppDefaultHostname
output couponBackendUrl string = couponBackendUrl
output orderBackendUrl string = orderBackendUrl
output appInsightsConnectionString string = observability.outputs.appInsightsConnectionString
