@description('Azure region for Container Apps.')
param location string

@description('Short environment name used in resource names.')
param environmentName string

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

@description('Public placeholder image so first deploy into an empty RG does not deadlock on an empty ACR (P-11).')
param placeholderImage string = 'mcr.microsoft.com/k8se/quickstart:latest'

@description('Resource tags. Must include project, env and owner.')
param tags object

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: logAnalyticsWorkspaceName
}

resource managedEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
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
    managedEnvironmentId: managedEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      activeRevisionsMode: 'Single'
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
    managedEnvironmentId: managedEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      activeRevisionsMode: 'Single'
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

output environmentId string = managedEnvironment.id
output couponAppFqdn string = couponApp.properties.configuration.ingress.fqdn
output orderAppFqdn string = orderApp.properties.configuration.ingress.fqdn
output couponAppName string = couponApp.name
output orderAppName string = orderApp.name
