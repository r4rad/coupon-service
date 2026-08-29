using './main.bicep'

param location = 'eastus2'
param staticWebAppLocation = 'eastus2'
param environmentName = 'demo'
param projectName = 'coupon-service'
param ownerTag = 'coupon-demo'
param hostingMode = 'containerApps'
param cosmosEnableFreeTier = true
param logAnalyticsDailyCapGb = 1
param placeholderImage = 'mcr.microsoft.com/k8se/quickstart:latest'
param apimPublisherEmail = 'noreply@example.com'
param apimPublisherName = 'Coupon Demo'
param entraTenantId = '00000000-0000-0000-0000-000000000000'
param couponApiAudience = 'api://coupon-service'
param spaOrigin = 'https://localhost:5173'
