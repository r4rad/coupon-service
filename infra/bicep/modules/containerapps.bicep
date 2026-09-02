@description('Azure region for Container Apps.')
param location string

@description('Short environment name used in resource names.')
param environmentName string

@description('Full resource id of an existing managed environment. When set, CAE creation is skipped (subscription global one-CAE quota).')
param existingManagedEnvironmentResourceId string = ''

@description('Log Analytics workspace name used for Container Apps diagnostics.')
param logAnalyticsWorkspaceName string

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

@description('User-assigned identity service principal object id for the order API (AC-7.7).')
param orderIdentityPrincipalId string

@description('Public placeholder image so first deploy into an empty RG does not deadlock on an empty ACR (P-11).')
param placeholderImage string = 'mcr.microsoft.com/k8se/quickstart:latest'

@description('ACR login server. Empty skips registry identity wiring until the first image push.')
param acrLoginServer string = ''

@description('Entra authority used by the Coupon Service JwtBearer middleware (AC-7.6).')
param jwtAuthority string

@description('Coupon Service API audience / Application ID URI.')
param couponApiAudience string

@description('Coupon Service app registration client id, the aud value of version 2 tokens.')
param couponApiClientId string = ''

@description('OAuth scope the Order API requests with managed identity (AC-7.7).')
param couponServiceScope string

@description('Seeds the deterministic policy set as the Coupon Service starts (AC-9.5, AC-9.6).')
param seedPoliciesOnStartup bool = true

@description('Expose Scalar, ReDoc, and /openapi on API hosts. Off in production.')
param enableApiDocumentation bool = false

@description('Resource tags. Must include project, env and owner.')
param tags object

var useExistingEnvironment = !empty(existingManagedEnvironmentResourceId)
var existingEnvironmentParts = split(existingManagedEnvironmentResourceId, '/')

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: logAnalyticsWorkspaceName
}

resource existingManagedEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' existing = if (useExistingEnvironment) {
  name: existingEnvironmentParts[8]
  scope: resourceGroup(existingEnvironmentParts[2], existingEnvironmentParts[4])
}

resource managedEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = if (!useExistingEnvironment) {
  name: 'cae-coupon-${environmentName}'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        // listKeys stays inside this module so shared keys are never module outputs (linter).
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

var managedEnvironmentId = useExistingEnvironment ? existingManagedEnvironment.id : managedEnvironment.id

resource couponApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-coupon-api-${environmentName}'
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${couponIdentityId}': {}
    }
  }
  properties: {
    managedEnvironmentId: managedEnvironmentId
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      activeRevisionsMode: 'Single'
      registries: acrLoginServer == '' ? [] : [
        {
          server: acrLoginServer
          identity: couponIdentityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'coupon-api'
          image: placeholderImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
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
            {
              name: 'Authentication__Jwt__TrustedRedeemPrincipalIds__0'
              value: orderIdentityPrincipalId
            }
            {
              name: 'Seeding__Enabled'
              value: string(seedPoliciesOnStartup)
            }
            {
              name: 'ApiDocumentation__Enabled'
              value: string(enableApiDocumentation)
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 3
      }
    }
  }
}

resource orderApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-order-api-${environmentName}'
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${orderIdentityId}': {}
    }
  }
  properties: {
    managedEnvironmentId: managedEnvironmentId
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      activeRevisionsMode: 'Single'
      registries: acrLoginServer == '' ? [] : [
        {
          server: acrLoginServer
          identity: orderIdentityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'order-api'
          image: placeholderImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
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
              value: 'https://${couponApp.properties.configuration.ingress.fqdn}'
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
            {
              name: 'ApiDocumentation__Enabled'
              value: string(enableApiDocumentation)
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 3
      }
    }
  }
}

output environmentId string = managedEnvironmentId
output couponAppFqdn string = couponApp.properties.configuration.ingress.fqdn
output orderAppFqdn string = orderApp.properties.configuration.ingress.fqdn
output couponAppName string = couponApp.name
output orderAppName string = orderApp.name
