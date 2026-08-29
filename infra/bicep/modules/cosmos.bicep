@description('Azure region for Cosmos DB.')
param location string

@description('Short environment name used in resource names.')
param environmentName string

@description('Suffix that keeps the account name globally unique without baking in a resource group name.')
param uniqueSuffix string

@description('Enable Cosmos free tier when the subscription still has the allotment available.')
param enableFreeTier bool = true

@description('Resource tags. Must include project, env and owner.')
param tags object

var accountName = take('cosmos-coupon-${environmentName}-${uniqueSuffix}', 44)

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' = {
  name: accountName
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    enableFreeTier: enableFreeTier
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
    minimalTlsVersion: 'Tls12'
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = {
  parent: cosmosAccount
  name: 'coupons'
  properties: {
    resource: {
      id: 'coupons'
    }
  }
}

resource policies 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'policies'
  properties: {
    resource: {
      id: 'policies'
      partitionKey: {
        paths: [
          '/pk'
        ]
        kind: 'Hash'
      }
    }
  }
}

resource redemptions 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'redemptions'
  properties: {
    resource: {
      id: 'redemptions'
      partitionKey: {
        paths: [
          '/pk'
        ]
        kind: 'Hash'
      }
      // Unique key enforces redemption idempotency by orderId (AC-4.x / design).
      uniqueKeyPolicy: {
        uniqueKeys: [
          {
            paths: [
              '/orderId'
            ]
          }
        ]
      }
      // Default TTL off; documents set ttl while Reserved and clear it on confirm.
      defaultTtl: -1
    }
  }
}

resource orders 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'orders'
  properties: {
    resource: {
      id: 'orders'
      partitionKey: {
        paths: [
          '/orderId'
        ]
        kind: 'Hash'
      }
    }
  }
}

output cosmosAccountId string = cosmosAccount.id
output cosmosAccountName string = cosmosAccount.name
output cosmosEndpoint string = cosmosAccount.properties.documentEndpoint
output databaseName string = database.name
