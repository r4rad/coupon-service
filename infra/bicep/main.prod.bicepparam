using './main.bicep'

param location = 'eastus2'
param staticWebAppLocation = 'eastus2'
param environmentName = 'prod'
param projectName = 'coupon-service'
param ownerTag = 'coupon-prod'
param hostingMode = 'containerApps'
// Subscription allows one CAE globally; prod apps attach to the develop CD environment in rg-coupon-demo.
param existingManagedEnvironmentResourceGroup = 'rg-coupon-demo'
param existingManagedEnvironmentName = 'cae-coupon-dev'
param cosmosEnableFreeTier = false
param logAnalyticsDailyCapGb = 1
param placeholderImage = 'mcr.microsoft.com/k8se/quickstart:latest'
param apimPublisherEmail = 'noreply@example.com'
param apimPublisherName = 'Coupon Prod'
param couponApiAudience = 'api://coupon-service'
param couponApiClientId = '189703ee-da8c-4fa4-8c0d-a53f193283f4'
param spaOrigin = 'https://localhost:5173'
