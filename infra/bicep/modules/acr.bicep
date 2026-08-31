@description('Azure region for Container Registry.')
param location string

@description('Suffix that keeps the registry name globally unique without baking in a resource group name.')
@minLength(5)
@maxLength(45)
param uniqueSuffix string

@description('Principal IDs granted AcrPull so Container Apps can pull pipeline-built images.')
param pullPrincipalIds array = []

@description('Resource tags. Must include project, env and owner.')
param tags object

// ACR names are alphanumeric only (5-50 chars). Prefix + minLength suffix satisfies the registry min length.
// Basic is the only paid SKU accepted by NFR-6.
var registryName = 'acr${uniqueSuffix}'

resource acr 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: registryName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

// AcrPull — well-known role definition id.
var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

resource pullAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for (principalId, i) in pullPrincipalIds: {
    name: guid(acr.id, principalId, acrPullRoleId)
    scope: acr
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
      principalId: principalId
      principalType: 'ServicePrincipal'
    }
  }
]

output acrId string = acr.id
output acrName string = acr.name
output acrLoginServer string = acr.properties.loginServer
