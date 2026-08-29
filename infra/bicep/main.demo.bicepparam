using './main.bicep'

// Demo parameters hold only non-sensitive configuration. The resource group is supplied by the
// deployment command (az deployment group … --resource-group …), never by this file.

param location = 'westeurope'
param staticWebAppLocation = 'westeurope'
param environmentName = 'demo'
param projectName = 'coupon-service'
param ownerTag = 'coupon-demo'
param hostingMode = 'containerApps'
param cosmosEnableFreeTier = true
param logAnalyticsDailyCapGb = 1
param placeholderImage = 'mcr.microsoft.com/k8se/quickstart:latest'
param apimPublisherEmail = 'noreply@example.com'
param apimPublisherName = 'Coupon Demo'
