@description('Azure region for Static Web Apps. Free SKU regions are limited.')
param location string

@description('Short environment name used in resource names.')
param environmentName string

@description('Resource tags. Must include project, env and owner.')
param tags object

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: 'swa-coupon-${environmentName}'
  location: location
  tags: tags
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    allowConfigFileUpdates: true
  }
}

output staticWebAppId string = staticWebApp.id
output staticWebAppName string = staticWebApp.name
output staticWebAppDefaultHostname string = staticWebApp.properties.defaultHostname
