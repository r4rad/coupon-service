@description('Azure region for managed identities.')
param location string

@description('Short environment name used in resource names.')
param environmentName string

@description('Resource tags. Must include project, env and owner.')
param tags object

resource couponIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-coupon-api-${environmentName}'
  location: location
  tags: tags
}

resource orderIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-order-api-${environmentName}'
  location: location
  tags: tags
}

output couponIdentityId string = couponIdentity.id
output couponIdentityPrincipalId string = couponIdentity.properties.principalId
output couponIdentityClientId string = couponIdentity.properties.clientId
output orderIdentityId string = orderIdentity.id
output orderIdentityPrincipalId string = orderIdentity.properties.principalId
output orderIdentityClientId string = orderIdentity.properties.clientId
