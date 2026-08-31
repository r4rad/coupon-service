@description('Azure region for App Service fallback hosts.')
param location string

@description('Short environment name used in resource names.')
param environmentName string

@description('Suffix that keeps App Service host names globally unique without baking in a resource group name.')
param uniqueSuffix string

@description('Application Insights connection string injected into both apps.')
@secure()
param appInsightsConnectionString string

@description('User-assigned identity resource id for the coupon API.')
param couponIdentityId string

@description('User-assigned identity client id for the coupon API.')
param couponIdentityClientId string

@description('User-assigned identity resource id for the order API.')
param orderIdentityId string

@description('User-assigned identity client id for the order API.')
param orderIdentityClientId string

@description('Entra authority used by the Coupon Service JwtBearer middleware (AC-7.6).')
param jwtAuthority string

@description('Coupon Service API audience / Application ID URI.')
param couponApiAudience string

@description('Coupon Service app registration client id, the aud value of version 2 tokens.')
param couponApiClientId string = ''

@description('OAuth scope the Order API requests with managed identity (AC-7.7).')
param couponServiceScope string

@description('Resource tags. Must include project, env and owner.')
param tags object

// F1 is the strict-zero-cost fallback when Container Apps (and ACR) are declined.
resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'plan-coupon-${environmentName}'
  location: location
  tags: tags
  sku: {
    name: 'F1'
    tier: 'Free'
    capacity: 1
  }
  properties: {
    reserved: false
  }
}

resource couponApp 'Microsoft.Web/sites@2023-12-01' = {
  name: take('app-coupon-api-${environmentName}-${uniqueSuffix}', 60)
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${couponIdentityId}': {}
    }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'AZURE_CLIENT_ID'
          value: couponIdentityClientId
        }
        {
          name: 'Authentication__Jwt__Authority'
          value: jwtAuthority
        }
        {
          name: 'Authentication__Jwt__Audience'
          value: couponApiAudience
        }
        {
          name: 'Authentication__Jwt__ClientId'
          value: couponApiClientId
        }
        {
          name: 'Authentication__Jwt__Issuer'
          value: jwtAuthority
        }
        {
          name: 'Authentication__TestToken__Enabled'
          value: 'false'
        }
      ]
    }
  }
}

resource orderApp 'Microsoft.Web/sites@2023-12-01' = {
  name: take('app-order-api-${environmentName}-${uniqueSuffix}', 60)
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${orderIdentityId}': {}
    }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'AZURE_CLIENT_ID'
          value: orderIdentityClientId
        }
        {
          name: 'OrderApi__CouponServiceBaseUrl'
          value: 'https://${couponApp.properties.defaultHostName}'
        }
        {
          name: 'OrderApi__UseManagedIdentity'
          value: 'true'
        }
        {
          name: 'OrderApi__CouponServiceResource'
          value: couponApiAudience
        }
        {
          name: 'OrderApi__CouponServiceScope'
          value: couponServiceScope
        }
      ]
    }
  }
}

output planId string = plan.id
output couponAppHostName string = couponApp.properties.defaultHostName
output orderAppHostName string = orderApp.properties.defaultHostName
output couponAppName string = couponApp.name
output orderAppName string = orderApp.name
