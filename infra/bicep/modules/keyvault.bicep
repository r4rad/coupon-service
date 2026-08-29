@description('Azure region for Key Vault.')
param location string

@description('Short environment name used in resource names.')
param environmentName string

@description('Suffix that keeps the vault name globally unique without baking in a resource group name.')
param uniqueSuffix string

@description('Principal IDs granted Key Vault Secrets User.')
param secretsUserPrincipalIds array

@description('Resource tags. Must include project, env and owner.')
param tags object

// Vault names are 3-24 chars, alphanumeric and hyphen.
var vaultName = take('kv-coupon-${environmentName}-${uniqueSuffix}', 24)

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: vaultName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: tenant().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enablePurgeProtection: false
    publicNetworkAccess: 'Enabled'
  }
}

// Key Vault Secrets User — role definition id is well-known and stable.
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource secretsUserAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for (principalId, i) in secretsUserPrincipalIds: {
    name: guid(keyVault.id, principalId, keyVaultSecretsUserRoleId)
    scope: keyVault
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
      principalId: principalId
      principalType: 'ServicePrincipal'
    }
  }
]

output keyVaultId string = keyVault.id
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
