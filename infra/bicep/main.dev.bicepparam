using './main.bicep'

param location = 'eastus2'
param staticWebAppLocation = 'eastus2'
param environmentName = 'dev'
param projectName = 'coupon-service'
param ownerTag = 'coupon-demo'
param hostingMode = 'containerApps'
param cosmosEnableFreeTier = false
param logAnalyticsDailyCapGb = 1
param placeholderImage = 'mcr.microsoft.com/k8se/quickstart:latest'
param apimPublisherEmail = 'noreply@example.com'
param apimPublisherName = 'Coupon Demo'
param couponApiAudience = 'api://coupon-service'
param couponApiClientId = '189703ee-da8c-4fa4-8c0d-a53f193283f4'
param enableApiDocumentation = true
param spaOrigin = 'https://localhost:5173'
