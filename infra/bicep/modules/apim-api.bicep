@description('Existing API Management service name.')
param apimName string

@description('HTTPS base URL of the coupon API backend.')
param couponBackendUrl string

@description('HTTPS base URL of the order API backend.')
param orderBackendUrl string

@description('Entra tenant id used for openid-config and issuer checks. Placeholder until apps are registered.')
param entraTenantId string

@description('JWT audience for the Coupon Service API (Application ID URI).')
param couponApiAudience string

@description('Allowed SPA origin for CORS on the customer product.')
param spaOrigin string

resource apim 'Microsoft.ApiManagement/service@2023-09-01-preview' existing = {
  name: apimName
}

resource entraTenantNamedValue 'Microsoft.ApiManagement/service/namedValues@2023-09-01-preview' = {
  parent: apim
  name: 'entra-tenant-id'
  properties: {
    displayName: 'entra-tenant-id'
    value: entraTenantId
    secret: false
  }
}

resource jwtAudienceNamedValue 'Microsoft.ApiManagement/service/namedValues@2023-09-01-preview' = {
  parent: apim
  name: 'jwt-audience'
  properties: {
    displayName: 'jwt-audience'
    value: couponApiAudience
    secret: false
  }
}

resource spaOriginNamedValue 'Microsoft.ApiManagement/service/namedValues@2023-09-01-preview' = {
  parent: apim
  name: 'spa-origin'
  properties: {
    displayName: 'spa-origin'
    value: spaOrigin
    secret: false
  }
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
    // Public surface only: preview + health. Reserve/confirm/release stay off APIM (AC-7.7 internal hop).
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

resource couponAdminApi 'Microsoft.ApiManagement/service/apis@2023-09-01-preview' = {
  parent: apim
  name: 'coupon-admin'
  properties: {
    displayName: 'Coupon Admin'
    path: 'admin'
    protocols: [
      'https'
    ]
    subscriptionRequired: true
    apiRevision: '1'
    serviceUrl: couponBackendUrl
  }
}

resource customerProduct 'Microsoft.ApiManagement/service/products@2023-09-01-preview' = {
  parent: apim
  name: 'customer'
  properties: {
    displayName: 'Customer'
    description: 'Customer-facing coupon preview and order submit'
    subscriptionRequired: false
    state: 'published'
  }
}

resource adminProduct 'Microsoft.ApiManagement/service/products@2023-09-01-preview' = {
  parent: apim
  name: 'admin'
  properties: {
    displayName: 'Admin'
    description: 'Campaign-manager policy administration'
    subscriptionRequired: true
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
  name: couponAdminApi.name
}

resource customerProductPolicy 'Microsoft.ApiManagement/service/products/policies@2023-09-01-preview' = {
  parent: customerProduct
  name: 'policy'
  properties: {
    format: 'rawxml'
    value: loadTextContent('../policies/customer-product.xml')
  }
  dependsOn: [
    entraTenantNamedValue
    jwtAudienceNamedValue
    spaOriginNamedValue
  ]
}

resource adminProductPolicy 'Microsoft.ApiManagement/service/products/policies@2023-09-01-preview' = {
  parent: adminProduct
  name: 'policy'
  properties: {
    format: 'rawxml'
    value: loadTextContent('../policies/admin-product.xml')
  }
  dependsOn: [
    entraTenantNamedValue
    jwtAudienceNamedValue
  ]
}

resource couponApiRateLimitPolicy 'Microsoft.ApiManagement/service/apis/policies@2023-09-01-preview' = {
  parent: couponApi
  name: 'policy'
  properties: {
    format: 'rawxml'
    value: loadTextContent('../policies/customer-api-rate-limit.xml')
  }
}

resource orderApiRateLimitPolicy 'Microsoft.ApiManagement/service/apis/policies@2023-09-01-preview' = {
  parent: orderApi
  name: 'policy'
  properties: {
    format: 'rawxml'
    value: loadTextContent('../policies/customer-api-rate-limit.xml')
  }
}

resource couponPreviewOperation 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: couponApi
  name: 'preview'
  properties: {
    displayName: 'Preview coupon'
    method: 'POST'
    urlTemplate: '/v1/coupons/preview'
    description: 'Advisory coupon evaluation'
  }
}

resource couponLiveOperation 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: couponApi
  name: 'health-live'
  properties: {
    displayName: 'Liveness'
    method: 'GET'
    urlTemplate: '/v1/health/live'
  }
}

resource couponReadyOperation 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: couponApi
  name: 'health-ready'
  properties: {
    displayName: 'Readiness'
    method: 'GET'
    urlTemplate: '/v1/health/ready'
  }
}

resource orderListPizzasOperation 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: orderApi
  name: 'list-pizzas'
  properties: {
    displayName: 'List pizzas'
    method: 'GET'
    urlTemplate: '/v1/pizzas'
  }
}

resource orderCreateOperation 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: orderApi
  name: 'create-order'
  properties: {
    displayName: 'Create order'
    method: 'POST'
    urlTemplate: '/v1/orders'
  }
}

resource orderGetOperation 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: orderApi
  name: 'get-order'
  properties: {
    displayName: 'Get order'
    method: 'GET'
    urlTemplate: '/v1/orders/{orderId}'
    templateParameters: [
      {
        name: 'orderId'
        type: 'string'
        required: true
      }
    ]
  }
}

resource adminListPoliciesOperation 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: couponAdminApi
  name: 'list-policies'
  properties: {
    displayName: 'List policies'
    method: 'GET'
    urlTemplate: '/v1/admin/policies'
  }
}

resource adminCreatePolicyOperation 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: couponAdminApi
  name: 'create-policy'
  properties: {
    displayName: 'Create policy'
    method: 'POST'
    urlTemplate: '/v1/admin/policies'
  }
}

resource adminGetPolicyOperation 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: couponAdminApi
  name: 'get-policy'
  properties: {
    displayName: 'Get policy'
    method: 'GET'
    urlTemplate: '/v1/admin/policies/{id}'
    templateParameters: [
      {
        name: 'id'
        type: 'string'
        required: true
      }
    ]
  }
}

resource adminUpdatePolicyOperation 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: couponAdminApi
  name: 'update-policy'
  properties: {
    displayName: 'Update policy'
    method: 'PUT'
    urlTemplate: '/v1/admin/policies/{id}'
    templateParameters: [
      {
        name: 'id'
        type: 'string'
        required: true
      }
    ]
  }
}

resource adminDeletePolicyOperation 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: couponAdminApi
  name: 'delete-policy'
  properties: {
    displayName: 'Archive policy'
    method: 'DELETE'
    urlTemplate: '/v1/admin/policies/{id}'
    templateParameters: [
      {
        name: 'id'
        type: 'string'
        required: true
      }
    ]
  }
}

resource adminManifestOperation 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: couponAdminApi
  name: 'manifest'
  properties: {
    displayName: 'Engine manifest'
    method: 'GET'
    urlTemplate: '/v1/policy-engine/manifest'
  }
}

output couponApiName string = couponApi.name
output couponAdminApiName string = couponAdminApi.name
output orderApiName string = orderApi.name
output customerProductName string = customerProduct.name
output adminProductName string = adminProduct.name
