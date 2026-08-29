@description('Azure region for API Management.')
param location string

@description('Short environment name used in resource names.')
param environmentName string

@description('Suffix that keeps the service name globally unique without baking in a resource group name.')
param uniqueSuffix string

@description('Publisher email for the APIM instance.')
param publisherEmail string

@description('Publisher display name for the APIM instance.')
param publisherName string

@description('Resource tags. Must include project, env and owner.')
param tags object

var serviceName = take('apim-coupon-${environmentName}-${uniqueSuffix}', 50)

// Consumption is the free-grant tier named in section 17; Developer is an explicit monthly charge.
resource apim 'Microsoft.ApiManagement/service@2023-09-01-preview' = {
  name: serviceName
  location: location
  tags: tags
  sku: {
    name: 'Consumption'
    capacity: 0
  }
  properties: {
    publisherEmail: publisherEmail
    publisherName: publisherName
  }
}

output apimId string = apim.id
output apimName string = apim.name
output apimGatewayUrl string = apim.properties.gatewayUrl
