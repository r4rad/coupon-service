# Generates Postman Collection v2.1 files for develop and production APIM gateways.
# Run from repo root: ./scripts/generate-postman-collections.ps1

$ErrorActionPreference = 'Stop'

$previewBody = @{
    code                = 'SAVE10'
    customerId          = 'postman-customer'
    confirmedOrderCount = 0
    cart                = @{
        lines = @(
            @{
                lineId    = 'line-1'
                pizzaId   = 'margherita'
                category  = 'classic'
                unitPrice = 20.00
                quantity  = 2
            }
        )
    }
} | ConvertTo-Json -Depth 6 -Compress

$previewRejectedBody = @{
    code                = 'OLDCODE'
    customerId          = 'postman-customer'
    confirmedOrderCount = 0
    cart                = @{
        lines = @(
            @{
                lineId    = 'line-1'
                pizzaId   = 'margherita'
                category  = 'classic'
                unitPrice = 20.00
                quantity  = 1
            }
        )
    }
} | ConvertTo-Json -Depth 6 -Compress

$orderBody = @{
    customerId   = 'postman-customer'
    couponCode   = 'SAVE10'
    clientTotal  = 1.00
    lines        = @(
        @{ pizzaId = 'margherita'; quantity = 2 }
        @{ pizzaId = 'bbq-chicken'; quantity = 1 }
    )
} | ConvertTo-Json -Depth 6 -Compress

function New-RequestItem {
    param(
        [string] $Name,
        [string] $Method,
        [string] $Url,
        [string] $Description,
        [string] $Auth = 'inherit',
        [string] $Body,
        [string[]] $TestScript,
        [hashtable[]] $ExtraHeaders
    )

    $headers = @(
        @{ key = 'Accept'; value = 'application/json' }
    )
    if ($Method -in @('POST', 'PUT', 'PATCH')) {
        $headers += @{ key = 'Content-Type'; value = 'application/json' }
    }
    foreach ($h in $ExtraHeaders) {
        $headers += $h
    }

    $request = @{
        method = $Method
        header = $headers
        url    = $Url
    }

    if ($Auth -eq 'noauth') {
        $request['auth'] = @{ type = 'noauth' }
    }

    if (-not [string]::IsNullOrWhiteSpace($Body)) {
        $request['body'] = @{
            mode = 'raw'
            raw  = $Body
        }
    }

    $item = @{
        name    = $Name
        request = $request
        response = @()
    }

    if ($Description) {
        $item['description'] = $Description
    }

    if ($TestScript -and $TestScript.Count -gt 0) {
        $item['event'] = @(
            @{
                listen = 'test'
                script = @{
                    type = 'text/javascript'
                    exec = $TestScript
                }
            }
        )
    }

    return $item
}

function New-Collection {
    param(
        [string] $Name,
        [string] $EnvironmentLabel,
        [string] $ResourceGroup,
        [string] $GatewayPlaceholder
    )

    $statusOk = @(
        'pm.test("Status code is 200", function () {'
        '    pm.response.to.have.status(200);'
        '});'
    )

    $previewApplied = $statusOk + @(
        'const body = pm.response.json();'
        'pm.test("SAVE10 applied", function () {'
        '    pm.expect(body.status).to.eql("Applied");'
        '});'
        'pm.test("Discount is 4.00 on 40.00 basket", function () {'
        '    pm.expect(Number(body.pricing.discount)).to.eql(4);'
        '});'
    )

    $previewRejected = $statusOk + @(
        'const body = pm.response.json();'
        'pm.test("OLDCODE rejected", function () {'
        '    pm.expect(body.status).to.eql("Rejected");'
        '});'
    )

    $saveOrderId = @(
        'pm.test("Order created", function () {'
        '    pm.expect(pm.response.code).to.be.oneOf([200, 201]);'
        '});'
        'if (pm.response.code === 200 || pm.response.code === 201) {'
        '    const body = pm.response.json();'
        '    pm.collectionVariables.set("orderId", body.orderId);'
        '    pm.test("Server priced total", function () {'
        '        pm.expect(Number(body.total)).to.eql(27.9);'
        '    });'
        '}'
    )

    $getOrder = @(
        'pm.test("Status code is 200", function () {'
        '    pm.response.to.have.status(200);'
        '});'
        'const body = pm.response.json();'
        'pm.test("Order id matches", function () {'
        '    pm.expect(body.orderId).to.eql(pm.collectionVariables.get("orderId"));'
        '});'
    )

    $items = @(
        @{
            name        = '1 Health (no auth)'
            description = 'Anonymous probes — run first.'
            item        = @(
                (New-RequestItem -Name 'GET health/live' -Method 'GET' `
                    -Url '{{gatewayUrl}}/coupons/v1/health/live' `
                    -Description 'Liveness.' -Auth 'noauth' -TestScript $statusOk)
                (New-RequestItem -Name 'GET health/ready' -Method 'GET' `
                    -Url '{{gatewayUrl}}/coupons/v1/health/ready' `
                    -Description 'Readiness and startup seed check.' -Auth 'noauth' -TestScript $statusOk)
            )
        }
        @{
            name        = '2 Coupons (Bearer token)'
            description = 'Customer product through /coupons. Set collection variable bearerToken first.'
            item        = @(
                (New-RequestItem -Name 'POST preview — SAVE10' -Method 'POST' `
                    -Url '{{gatewayUrl}}/coupons/v1/coupons/preview' `
                    -Description 'Advisory preview; 10% off €40 basket.' -Body $previewBody -TestScript $previewApplied)
                (New-RequestItem -Name 'POST preview — OLDCODE rejected' -Method 'POST' `
                    -Url '{{gatewayUrl}}/coupons/v1/coupons/preview' `
                    -Description 'Business rejection still returns HTTP 200.' -Body $previewRejectedBody -TestScript $previewRejected)
            )
        }
        @{
            name        = '3 Orders (Bearer token)'
            description = 'Customer product through /orders. Run Place order before Get order.'
            item        = @(
                (New-RequestItem -Name 'GET pizzas catalog' -Method 'GET' `
                    -Url '{{gatewayUrl}}/orders/v1/pizzas' `
                    -Description 'Catalog from data/pizzas.json.' -TestScript @(
                        'pm.test("Status code is 200", function () { pm.response.to.have.status(200); });'
                        'pm.test("Has pizzas", function () {'
                        '    pm.expect(pm.response.json().pizzas.length).to.be.above(0);'
                        '});'
                    ))
                (New-RequestItem -Name 'POST place order — SAVE10' -Method 'POST' `
                    -Url '{{gatewayUrl}}/orders/v1/orders' `
                    -Description 'Server re-prices; clientTotal is ignored. Saves orderId variable.' `
                    -Body $orderBody -TestScript $saveOrderId)
                (New-RequestItem -Name 'GET order by id' -Method 'GET' `
                    -Url '{{gatewayUrl}}/orders/v1/orders/{{orderId}}' `
                    -Description 'Uses orderId from place order.' -TestScript $getOrder)
            )
        }
        @{
            name        = '4 Admin (optional)'
            description = 'Requires Coupon.Admin on the token AND admin APIM subscription key (Ocp-Apim-Subscription-Key).'
            item        = @(
                (New-RequestItem -Name 'GET admin policies' -Method 'GET' `
                    -Url '{{gatewayUrl}}/admin/v1/admin/policies' `
                    -Description 'List seeded policies.' -ExtraHeaders @(
                        @{ key = 'Ocp-Apim-Subscription-Key'; value = '{{adminSubscriptionKey}}' }
                    ))
                (New-RequestItem -Name 'GET policy engine manifest' -Method 'GET' `
                    -Url '{{gatewayUrl}}/admin/v1/policy-engine/manifest' `
                    -Description 'Engine capability manifest.' -ExtraHeaders @(
                        @{ key = 'Ocp-Apim-Subscription-Key'; value = '{{adminSubscriptionKey}}' }
                    ))
            )
        }
    )

    $description = @"
End-to-end tests for the **$EnvironmentLabel** stack through Azure APIM.

## Before you run

1. Set **gatewayUrl** to your APIM gateway root (no trailing slash).
   Discover: ``az apim list -g $ResourceGroup --query "[0].gatewayUrl" -o tsv``
2. Acquire a token and paste into **bearerToken**:
   ``az account get-access-token --resource api://coupon-service --query accessToken -o tsv``
3. (Optional admin folder) Set **adminSubscriptionKey** from APIM portal → Subscriptions → admin product.

## Suggested order

Run folder **1** → **2** → **3** top to bottom. Use Collection Runner for a one-click E2E pass.

PowerShell alternative: ``./scripts/test-deployed-apis.ps1 -GatewayUrl <gatewayUrl>``
"@

    return @{
        info = @{
            name        = $Name
            description = $description
            schema      = 'https://schema.getpostman.com/json/collection/v2.1.0/collection.json'
        }
        auth = @{
            type   = 'bearer'
            bearer = @(
                @{ key = 'token'; value = '{{bearerToken}}'; type = 'string' }
            )
        }
        variable = @(
            @{ key = 'gatewayUrl'; value = $GatewayPlaceholder }
            @{ key = 'bearerToken'; value = '' }
            @{ key = 'orderId'; value = '' }
            @{ key = 'tokenAudience'; value = 'api://coupon-service' }
            @{ key = 'couponApiClientId'; value = '189703ee-da8c-4fa4-8c0d-a53f193283f4' }
            @{ key = 'adminSubscriptionKey'; value = '' }
        )
        item = $items
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $repoRoot 'postman'

function Get-GatewayUrl {
    param([string] $ResourceGroup, [string] $Fallback)
    if (Get-Command az -ErrorAction SilentlyContinue) {
        $url = az apim list -g $ResourceGroup --query "[0].gatewayUrl" -o tsv 2>$null
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($url)) {
            return $url.Trim()
        }
    }
    return $Fallback
}

$devGateway = Get-GatewayUrl -ResourceGroup 'rg-coupon-demo' `
    -Fallback 'https://REPLACE-WITH-DEV-APIM.azure-api.net'
$prodGateway = Get-GatewayUrl -ResourceGroup 'rg-coupon-prod' `
    -Fallback 'https://REPLACE-WITH-PROD-APIM.azure-api.net'

$dev = New-Collection `
    -Name 'Coupon Service — Develop (rg-coupon-demo)' `
    -EnvironmentLabel 'develop / non-prod' `
    -ResourceGroup 'rg-coupon-demo' `
    -GatewayPlaceholder $devGateway

$prod = New-Collection `
    -Name 'Coupon Service — Production (rg-coupon-prod)' `
    -EnvironmentLabel 'main / production' `
    -ResourceGroup 'rg-coupon-prod' `
    -GatewayPlaceholder $prodGateway

$devPath = Join-Path $outDir 'coupon-service-develop.postman_collection.json'
$prodPath = Join-Path $outDir 'coupon-service-production.postman_collection.json'

$dev | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $devPath -Encoding utf8
$prod | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $prodPath -Encoding utf8

Write-Host "Wrote $devPath"
Write-Host "Wrote $prodPath"
