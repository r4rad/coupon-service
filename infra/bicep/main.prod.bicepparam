using './main.bicep'

param location = 'eastus2'
param staticWebAppLocation = 'eastus2'
param environmentName = 'prod'
param projectName = 'coupon-service'
param ownerTag = 'coupon-prod'
param hostingMode = 'appService'
param cosmosEnableFreeTier = false
param logAnalyticsDailyCapGb = 1
param placeholderImage = 'mcr.microsoft.com/k8se/quickstart:latest'
param apimPublisherEmail = 'noreply@example.com'
param apimPublisherName = 'Coupon Prod'
param couponApiAudience = 'api://coupon-service'
param spaOrigin = 'https://localhost:5173'
