# Generates Postman Collection v2.1 files for develop and production APIM gateways.
# Run from repo root: ./scripts/generate-postman-collections.ps1

$ErrorActionPreference = 'Stop'

function ConvertTo-PreviewBodyJson {
    param(
        [string] $Code,
        [string] $CustomerId = 'postman-customer',
        [int] $ConfirmedOrderCount = 0,
        [array] $Lines
    )

    return (@{
            code                = $Code
            customerId          = $CustomerId
            confirmedOrderCount = $ConfirmedOrderCount
            cart                = @{ lines = $Lines }
        } | ConvertTo-Json -Depth 6 -Compress)
}

function ConvertTo-OrderBodyJson {
    param(
        [string] $Code,
        [string] $CustomerId = 'postman-customer',
        [array] $Lines
    )

    return (@{
            customerId  = $CustomerId
            couponCode  = $Code
            clientTotal = 1.00
            lines       = $Lines
        } | ConvertTo-Json -Depth 6 -Compress)
}

function New-PreviewLine {
    param(
        [string] $LineId,
        [string] $PizzaId,
        [string] $Category,
        [decimal] $UnitPrice,
        [int] $Quantity
    )

    return @{
        lineId    = $LineId
        pizzaId   = $PizzaId
        category  = $Category
        unitPrice = $UnitPrice
        quantity  = $Quantity
    }
}

function New-OrderLine {
    param(
        [string] $PizzaId,
        [int] $Quantity
    )

    return @{
        pizzaId   = $PizzaId
        quantity  = $Quantity
    }
}

function New-StatusOkTestScript {
    return @(
        'pm.test("Status code is 200", function () {'
        '    pm.response.to.have.status(200);'
        '});'
    )
}

function New-PreviewAppliedTestScript {
    param(
        [string] $Code,
        [decimal] $ExpectedDiscount
    )

    return (New-StatusOkTestScript) + @(
        'const body = pm.response.json();'
        "pm.test('$Code applied', function () {"
        '    pm.expect(body.status).to.eql("Applied");'
        '});'
        "pm.test('Discount is $ExpectedDiscount', function () {"
        '    pm.expect(Number(body.pricing.discount)).to.eql(' + $ExpectedDiscount.ToString([System.Globalization.CultureInfo]::InvariantCulture) + ');'
        '});'
    )
}

function New-PreviewRejectedTestScript {
    param([string] $Code)

    return (New-StatusOkTestScript) + @(
        'const body = pm.response.json();'
        "pm.test('$Code rejected', function () {"
        '    pm.expect(body.status).to.eql("Rejected");'
        '});'
    )
}

function New-PreviewAppliedOrRejectedTestScript {
    param([string] $Code)

    return (New-StatusOkTestScript) + @(
        'const body = pm.response.json();'
        "pm.test('$Code applied or cap reached', function () {"
        '    pm.expect(body.status).to.be.oneOf(["Applied", "Rejected"]);'
        '});'
        'if (pm.response.json().status === "Applied") {'
        '    pm.test("Discount present when applied", function () {'
        '        pm.expect(Number(pm.response.json().pricing.discount)).to.be.above(0);'
        '    });'
        '}'
    )
}

function New-AdminSkipPrerequestScript {
    return @(
        'const key = pm.collectionVariables.get("adminSubscriptionKey");'
        'if (!key) {'
        '    pm.execution.skipRequest();'
        '}'
    )
}

function New-OrderIdSkipPrerequestScript {
    param([string] $VariableName = 'orderId')

    return @(
        "const id = pm.collectionVariables.get('$VariableName');"
        'if (!id) {'
        '    pm.execution.skipRequest();'
        '}'
    )
}

function New-AdminOkTestScript {
    return (New-StatusOkTestScript) + @(
        'const body = pm.response.json();'
        'pm.test("Response is JSON", function () {'
        '    pm.expect(body).to.be.an("object");'
        '});'
    )
}

function New-OrderCreatedTestScript {
    param(
        [decimal] $ExpectedTotal,
        [string] $VariableName = 'orderId'
    )

    $totalLiteral = $ExpectedTotal.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    return @(
        'pm.test("Order created", function () {'
        '    pm.expect(pm.response.code).to.be.oneOf([200, 201]);'
        '});'
        'if (pm.response.code === 200 || pm.response.code === 201) {'
        '    const body = pm.response.json();'
        "    pm.collectionVariables.set('$VariableName', body.orderId);"
        "    pm.test('Server priced total $totalLiteral', function () {"
        "        pm.expect(Number(body.total)).to.eql($totalLiteral);"
        '    });'
        '}'
    )
}

function New-RequestItem {
    param(
        [string] $Name,
        [string] $Method,
        [string] $Url,
        [string] $Description,
        [string] $Auth = 'inherit',
        [string] $Body,
        [string[]] $TestScript,
        [string[]] $PrerequestScript,
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
        name     = $Name
        request  = $request
        response = @()
    }

    if ($Description) {
        $item['description'] = $Description
    }

    $events = [System.Collections.Generic.List[object]]::new()
    if ($PrerequestScript -and $PrerequestScript.Count -gt 0) {
        $events.Add(@{
                listen = 'prerequest'
                script = @{
                    type = 'text/javascript'
                    exec = $PrerequestScript
                }
            })
    }

    if ($TestScript -and $TestScript.Count -gt 0) {
        $events.Add(@{
                listen = 'test'
                script = @{
                    type = 'text/javascript'
                    exec = $TestScript
                }
            })
    }

    if ($events.Count -gt 0) {
        $item['event'] = @($events)
    }

    return $item
}

function New-PreviewRequest {
    param(
        [string] $Name,
        [string] $Description,
        [string] $Body,
        [string[]] $TestScript
    )

    return New-RequestItem -Name $Name -Method 'POST' `
        -Url '{{gatewayUrl}}/coupons/v1/coupons/preview' `
        -Description $Description -Body $Body -TestScript $TestScript
}

function New-OrderRequest {
    param(
        [string] $Name,
        [string] $Description,
        [string] $Body,
        [string[]] $TestScript
    )

    return New-RequestItem -Name $Name -Method 'POST' `
        -Url '{{gatewayUrl}}/orders/v1/orders' `
        -Description $Description -Body $Body -TestScript $TestScript
}

function New-Collection {
    param(
        [string] $Name,
        [string] $EnvironmentLabel,
        [string] $ResourceGroup,
        [string] $GatewayPlaceholder
    )

    $statusOk = New-StatusOkTestScript

    $standardBasketPreview = @(
        (New-PreviewLine -LineId 'line-1' -PizzaId 'margherita' -Category 'classic' -UnitPrice 20.00 -Quantity 2)
    )

    $flat5PreviewLines = @(
        (New-PreviewLine -LineId 'line-1' -PizzaId 'margherita' -Category 'classic' -UnitPrice 20.00 -Quantity 2)
    )

    $veggie15PreviewLines = @(
        (New-PreviewLine -LineId 'line-1' -PizzaId 'margherita' -Category 'Vegetarian' -UnitPrice 12.50 -Quantity 2)
        (New-PreviewLine -LineId 'line-2' -PizzaId 'quattro-formaggi' -Category 'Vegetarian' -UnitPrice 12.50 -Quantity 2)
    )

    $bogoPreviewLines = @(
        (New-PreviewLine -LineId 'line-1' -PizzaId 'margherita' -Category 'classic' -UnitPrice 10.00 -Quantity 2)
    )

    $eitherPreviewLines = @(
        (New-PreviewLine -LineId 'line-1' -PizzaId 'margherita' -Category 'Vegetarian' -UnitPrice 14.50 -Quantity 2)
    )

    $oldCodePreviewLines = @(
        (New-PreviewLine -LineId 'line-1' -PizzaId 'margherita' -Category 'classic' -UnitPrice 20.00 -Quantity 1)
    )

    $limited1PreviewLines = @(
        (New-PreviewLine -LineId 'line-1' -PizzaId 'margherita' -Category 'classic' -UnitPrice 20.00 -Quantity 2)
    )

    $checkoutBasket = @(
        (New-OrderLine -PizzaId 'margherita' -Quantity 2)
        (New-OrderLine -PizzaId 'bbq-chicken' -Quantity 1)
    )

    $flat5OrderLines = $checkoutBasket
    $eitherOrderLines = $checkoutBasket
    $bogoOrderLines = @(
        (New-OrderLine -PizzaId 'margherita' -Quantity 2)
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

    $couponPreviewItems = @(
        (New-PreviewRequest -Name 'POST preview — SAVE10 (10% off)' `
            -Description '10% off eligible lines. Basket €40 → €4 discount.' `
            -Body (ConvertTo-PreviewBodyJson -Code 'SAVE10' -Lines $standardBasketPreview) `
            -TestScript (New-PreviewAppliedTestScript -Code 'SAVE10' -ExpectedDiscount 4))
        (New-PreviewRequest -Name 'POST preview — FLAT5 (€5 off ≥ €20)' `
            -Description 'Fixed €5 when subtotal ≥ €20.' `
            -Body (ConvertTo-PreviewBodyJson -Code 'FLAT5' -Lines $flat5PreviewLines) `
            -TestScript (New-PreviewAppliedTestScript -Code 'FLAT5' -ExpectedDiscount 5))
        (New-PreviewRequest -Name 'POST preview — VEGGIE15 (15% veg, cap €10)' `
            -Description 'Vegetarian lines only; subtotal ≥ €25; first order or weekend. Uses category Vegetarian in cart.' `
            -Body (ConvertTo-PreviewBodyJson -Code 'VEGGIE15' -ConfirmedOrderCount 0 -Lines $veggie15PreviewLines) `
            -TestScript (New-PreviewAppliedTestScript -Code 'VEGGIE15' -ExpectedDiscount 7.5))
        (New-PreviewRequest -Name 'POST preview — BOGO (2nd item free)' `
            -Description 'Buy-one-get-one on second eligible line (qty ≥ 2).' `
            -Body (ConvertTo-PreviewBodyJson -Code 'BOGO' -Lines $bogoPreviewLines) `
            -TestScript (New-PreviewAppliedTestScript -Code 'BOGO' -ExpectedDiscount 10))
        (New-PreviewRequest -Name 'POST preview — EITHER (best of % or flat)' `
            -Description 'Best of 15% or €5; €29 basket picks €5.' `
            -Body (ConvertTo-PreviewBodyJson -Code 'EITHER' -Lines $eitherPreviewLines) `
            -TestScript (New-PreviewAppliedTestScript -Code 'EITHER' -ExpectedDiscount 5))
        (New-PreviewRequest -Name 'POST preview — OLDCODE rejected' `
            -Description 'Expired policy window — HTTP 200 with status Rejected.' `
            -Body (ConvertTo-PreviewBodyJson -Code 'OLDCODE' -Lines $oldCodePreviewLines) `
            -TestScript (New-PreviewRejectedTestScript -Code 'OLDCODE'))
        (New-PreviewRequest -Name 'POST preview — LIMITED1 (global cap 1)' `
            -Description 'Applied until the single global redemption is consumed; may be Rejected on prod after first checkout.' `
            -Body (ConvertTo-PreviewBodyJson -Code 'LIMITED1' -Lines $limited1PreviewLines) `
            -TestScript (New-PreviewAppliedOrRejectedTestScript -Code 'LIMITED1'))
    )

    $orderCheckoutItems = @(
        (New-OrderRequest -Name 'POST place order — SAVE10' `
            -Description 'margherita×2 + bbq-chicken×1 → subtotal €31, 10% off → total €27.90.' `
            -Body (ConvertTo-OrderBodyJson -Code 'SAVE10' -Lines $checkoutBasket) `
            -TestScript (New-OrderCreatedTestScript -ExpectedTotal 27.9 -VariableName 'orderId'))
        (New-OrderRequest -Name 'POST place order — FLAT5' `
            -Description 'Same basket; €5 flat off → total €26.00.' `
            -Body (ConvertTo-OrderBodyJson -Code 'FLAT5' -CustomerId 'postman-flat5' -Lines $flat5OrderLines) `
            -TestScript (New-OrderCreatedTestScript -ExpectedTotal 26 -VariableName 'orderIdFlat5'))
        (New-OrderRequest -Name 'POST place order — BOGO' `
            -Description 'margherita×2 → second pizza free → total €9.50.' `
            -Body (ConvertTo-OrderBodyJson -Code 'BOGO' -CustomerId 'postman-bogo' -Lines $bogoOrderLines) `
            -TestScript (New-OrderCreatedTestScript -ExpectedTotal 9.5 -VariableName 'orderIdBogo'))
        (New-OrderRequest -Name 'POST place order — EITHER' `
            -Description 'Same basket as SAVE10; best of 15% vs €5 → total €26.00.' `
            -Body (ConvertTo-OrderBodyJson -Code 'EITHER' -CustomerId 'postman-either' -Lines $eitherOrderLines) `
            -TestScript (New-OrderCreatedTestScript -ExpectedTotal 26 -VariableName 'orderIdEither'))
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
            name        = '2 Coupons — all seeded codes (Bearer)'
            description = 'Preview every seeded code from SeedPolicies.json. Set bearerToken before running.'
            item        = $couponPreviewItems
        }
        @{
            name        = '3 Orders — checkout by coupon (Bearer)'
            description = 'Catalog + checkout per coupon. Requires Coupon.Redeem on Order API MI. Run after folder 2.'
            item        = @(
                (New-RequestItem -Name 'GET pizzas catalog' -Method 'GET' `
                    -Url '{{gatewayUrl}}/orders/v1/pizzas' `
                    -Description 'Catalog from data/pizzas.json.' -TestScript @(
                        'pm.test("Status code is 200", function () { pm.response.to.have.status(200); });'
                        'pm.test("Has pizzas", function () {'
                        '    pm.expect(pm.response.json().pizzas.length).to.be.above(0);'
                        '});'
                    ))
            ) + $orderCheckoutItems + @(
                (New-RequestItem -Name 'GET order by id (SAVE10)' -Method 'GET' `
                    -Url '{{gatewayUrl}}/orders/v1/orders/{{orderId}}' `
                    -Description 'Uses orderId from SAVE10 checkout. Skipped when checkout did not run.' `
                    -PrerequestScript (New-OrderIdSkipPrerequestScript -VariableName 'orderId') `
                    -TestScript $getOrder)
            )
        }
        @{
            name        = '4 Admin (optional)'
            description = 'Requires Coupon.Admin on the token AND admin APIM subscription key (Ocp-Apim-Subscription-Key).'
            item        = @(
                (New-RequestItem -Name 'GET admin policies' -Method 'GET' `
                    -Url '{{gatewayUrl}}/admin/v1/admin/policies' `
                    -Description 'List seeded policies. Skipped when adminSubscriptionKey is empty.' `
                    -PrerequestScript (New-AdminSkipPrerequestScript) `
                    -TestScript (New-AdminOkTestScript) -ExtraHeaders @(
                        @{ key = 'Ocp-Apim-Subscription-Key'; value = '{{adminSubscriptionKey}}' }
                    ))
                (New-RequestItem -Name 'GET policy engine manifest' -Method 'GET' `
                    -Url '{{gatewayUrl}}/admin/v1/policy-engine/manifest' `
                    -Description 'Engine capability manifest. Skipped when adminSubscriptionKey is empty.' `
                    -PrerequestScript (New-AdminSkipPrerequestScript) `
                    -TestScript (New-AdminOkTestScript) -ExtraHeaders @(
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

Run folder **1** → **2** (all coupon previews) → **3** (checkout scenarios). Use Collection Runner.

Folder **4** is skipped automatically unless you set **adminSubscriptionKey**.

**LIMITED1** preview may return **Rejected** on production after the global cap is consumed.

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
            @{ key = 'orderIdFlat5'; value = '' }
            @{ key = 'orderIdBogo'; value = '' }
            @{ key = 'orderIdEither'; value = '' }
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
