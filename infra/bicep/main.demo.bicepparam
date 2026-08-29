using './main.bicep'

param location = 'eastus2'
param staticWebAppLocation = 'eastus2'
param environmentName = 'demo'
param projectName = 'coupon-service'
param ownerTag = 'coupon-demo'
param hostingMode = 'containerApps'
// Subscription already has a free-tier Cosmos account (CS-27 apply); serverless without free tier.
param cosmosEnableFreeTier = false
param logAnalyticsDailyCapGb = 1
param placeholderImage = 'mcr.microsoft.com/k8se/quickstart:latest'
param apimPublisherEmail = 'noreply@example.com'
param apimPublisherName = 'Coupon Demo'
param couponApiAudience = 'api://coupon-service'
param spaOrigin = 'https://localhost:5173'
