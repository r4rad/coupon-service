@description('Existing API Management service name.')
param apimName string

@description('HTTPS base URL of the coupon API backend.')
param couponBackendUrl string

@description('HTTPS base URL of the order API backend.')
param orderBackendUrl string

resource apim 'Microsoft.ApiManagement/service@2023-09-01-preview' existing = {
  name: apimName
}

resource couponBackend 'Microsoft.ApiManagement/service/backends@2023-09-01-preview' = {
  parent: apim
  name: 'coupon-api'
  properties: {
    url: couponBackendUrl
    protocol: 'http'
    description: 'Coupon Service backend'
  }
}

resource orderBackend 'Microsoft.ApiManagement/service/backends@2023-09-01-preview' = {
  parent: apim
  name: 'order-api'
  properties: {
    url: orderBackendUrl
    protocol: 'http'
    description: 'Order API backend'
  }
}

resource couponApi 'Microsoft.ApiManagement/service/apis@2023-09-01-preview' = {
  parent: apim
  name: 'coupon-service'
  properties: {
    displayName: 'Coupon Service'
    path: 'coupons'
    protocols: [
      'https'
    ]
    subscriptionRequired: false
    apiRevision: '1'
    // JWT validation and rate limiting are applied in CS-28; this ticket wires the API surface only.
    serviceUrl: couponBackendUrl
  }
}

resource orderApi 'Microsoft.ApiManagement/service/apis@2023-09-01-preview' = {
  parent: apim
  name: 'order-service'
  properties: {
    displayName: 'Order Service'
    path: 'orders'
    protocols: [
      'https'
    ]
    subscriptionRequired: false
    apiRevision: '1'
    serviceUrl: orderBackendUrl
  }
}

resource customerProduct 'Microsoft.ApiManagement/service/products@2023-09-01-preview' = {
  parent: apim
  name: 'customer'
  properties: {
    displayName: 'Customer'
    description: 'Customer-facing coupon preview and order submit'
    subscriptionRequired: false
    approvalRequired: false
    state: 'published'
  }
}

resource adminProduct 'Microsoft.ApiManagement/service/products@2023-09-01-preview' = {
  parent: apim
  name: 'admin'
  properties: {
    displayName: 'Admin'
    description: 'Campaign-manager policy administration'
    subscriptionRequired: false
    approvalRequired: false
    state: 'published'
  }
}

resource customerCouponLink 'Microsoft.ApiManagement/service/products/apis@2023-09-01-preview' = {
  parent: customerProduct
  name: couponApi.name
}

resource customerOrderLink 'Microsoft.ApiManagement/service/products/apis@2023-09-01-preview' = {
  parent: customerProduct
  name: orderApi.name
}

resource adminCouponLink 'Microsoft.ApiManagement/service/products/apis@2023-09-01-preview' = {
  parent: adminProduct
  name: couponApi.name
}

output couponApiName string = couponApi.name
output orderApiName string = orderApi.name
output customerProductName string = customerProduct.name
output adminProductName string = adminProduct.name
