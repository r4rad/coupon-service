# Testing the deployed APIs (develop and production)

Step-by-step guide for manually verifying **develop** (`rg-coupon-demo`) and **production** (`rg-coupon-prod`) after a green CD run. Use the PowerShell scripts for automation or the Postman collections for interactive testing.

You call **Azure API Management (APIM)** only — not Container Apps directly.

| Environment | Branch | Resource group | Postman collection |
|-------------|--------|----------------|--------------------|
| Develop | `develop` | `rg-coupon-demo` | [`postman/coupon-service-develop.postman_collection.json`](../postman/coupon-service-develop.postman_collection.json) |
| Production | `main` | `rg-coupon-prod` | [`postman/coupon-service-production.postman_collection.json`](../postman/coupon-service-production.postman_collection.json) |

Related: [deployment.md](deployment.md), [authentication.md](authentication.md), [pipeline-prerequisites.md](pipeline-prerequisites.md).

---

## 1. Prerequisites

| Tool | Purpose |
|------|---------|
| [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) | Login, discover gateway URLs, acquire JWTs |
| PowerShell 5.1+ or `pwsh` | Run scripts in `scripts/` |
| [Postman](https://www.postman.com/downloads/) (optional) | Import collections and use Collection Runner |

One-time Entra setup (required before tokens work):

```powershell
cd <repo-root>
./scripts/setup-entra-app.ps1
```

The script creates the Coupon Service API app registration, exposes delegated scope `access_as_user`, pre-authorizes Microsoft Azure CLI, and grants tenant-wide consent so this command works:

```powershell
az account get-access-token --resource api://coupon-service --query accessToken -o tsv
```

See [pipeline-prerequisites.md](pipeline-prerequisites.md) section 4 for `AdminPrincipalId` / `RedeemPrincipalId` when you need admin or checkout hops.

---

## 2. Log in to Azure

```powershell
az login --tenant <your-tenant-id>
az account set --subscription "<subscription-id-or-name>"
az account show --query "{name:name, id:id}" -o table
```

**Do not** run `az login --scope api://coupon-service/.default`. Login normally, then request the API token in step 3.

---

## 3. Discover gateway URLs

```powershell
# Develop
az apim list -g rg-coupon-demo --query "[0].gatewayUrl" -o tsv

# Production
az apim list -g rg-coupon-prod --query "[0].gatewayUrl" -o tsv
```

Save as variables:

```powershell
$devGateway  = "https://apim-coupon-dev-XXXX.azure-api.net"   # paste yours
$prodGateway = "https://apim-coupon-prod-XXXX.azure-api.net" # paste yours
```

Postman collections ship with `gatewayUrl` collection variables. After import, open **Variables** and confirm the hostname matches your deployment (or re-run `./scripts/generate-postman-collections.ps1` to auto-fill from `az apim list`).

### URL structure

| Product | Gateway prefix | Example |
|---------|----------------|---------|
| Coupons | `{gateway}/coupons` | `…/coupons/v1/coupons/preview` |
| Orders | `{gateway}/orders` | `…/orders/v1/pizzas` |
| Admin | `{gateway}/admin` | `…/admin/v1/admin/policies` |

---

## 4. Acquire a bearer token

```powershell
$token = az account get-access-token --resource api://coupon-service --query accessToken -o tsv
```

Decode at [jwt.ms](https://jwt.ms) and confirm:

| Claim | Expected |
|--------|----------|
| `aud` | `189703ee-da8c-4fa4-8c0d-a53f193283f4` (or your `couponApiClientId`) |
| `iss` | `https://login.microsoftonline.com/<tenant>/v2.0` |
| `scp` | `access_as_user` |

**Wrong token** (from plain `az account get-access-token` with no `--resource`):

| Claim | Wrong value |
|--------|-------------|
| `aud` | `https://management.core.windows.net/` |

| Error | Fix |
|-------|-----|
| `AADSTS500011` | Run `./scripts/setup-entra-app.ps1` |
| `AADSTS65001` / `consent_required` | Re-run `./scripts/setup-entra-app.ps1` (adds CLI scope + consent) |
| **401** at APIM with a fresh token | Check URL includes `/coupons/` or `/orders/` prefix |

Health endpoints are **anonymous** — no token:

```powershell
Invoke-RestMethod "$devGateway/coupons/v1/health/live"
Invoke-RestMethod "$devGateway/coupons/v1/health/ready"
```

---

## 5. PowerShell: automated end-to-end test

[`scripts/test-deployed-apis.ps1`](../scripts/test-deployed-apis.ps1) runs health, auth checks, preview, catalog, place order, and fetch order.

### Develop

```powershell
./scripts/test-deployed-apis.ps1 -GatewayUrl $devGateway
```

### Production

```powershell
./scripts/test-deployed-apis.ps1 -GatewayUrl $prodGateway
```

### Options

| Parameter | Default | Purpose |
|-----------|---------|---------|
| `-GatewayUrl` | *(required)* | APIM root only — no `/coupons` suffix |
| `-BearerToken` | from `az` | Skip token acquisition |
| `-CouponCode` | `SAVE10` | Seeded policy to exercise |
| `-SkipOrder` | off | Health + preview + catalog only |
| `-Audience` | `api://coupon-service` | Token resource |

Examples:

```powershell
# Preview and catalog only (no checkout)
./scripts/test-deployed-apis.ps1 -GatewayUrl $devGateway -SkipOrder

# Pre-acquired token
./scripts/test-deployed-apis.ps1 -GatewayUrl $devGateway -BearerToken $token

# Another seeded code
./scripts/test-deployed-apis.ps1 -GatewayUrl $devGateway -CouponCode FLAT5
```

**Checkout** (place order) needs `Coupon.Redeem` on the Order API managed identity:

```powershell
$redeemDev = az identity show -g rg-coupon-demo -n id-order-api-dev --query principalId -o tsv
./scripts/setup-entra-app.ps1 -RedeemPrincipalId $redeemDev
```

Repeat for `id-order-api-prod` in `rg-coupon-prod` before testing production checkout.

### CI parity smoke (no order placement)

[`scripts/smoke-deployed-stack.ps1`](../scripts/smoke-deployed-stack.ps1) — same auth and preview checks the pipeline runs in stage 7:

```powershell
./scripts/smoke-deployed-stack.ps1 `
  -CouponBaseUrl "$devGateway/coupons" `
  -OrderBaseUrl "$devGateway/orders" `
  -Audience api://coupon-service
```

Requires PowerShell 7+.

---

## 6. Postman: interactive end-to-end test

### Import

1. Postman → **Import** → select one or both:
   - `postman/coupon-service-develop.postman_collection.json`
   - `postman/coupon-service-production.postman_collection.json`
2. Open the collection → **Variables** tab.

### Set variables

| Variable | How to set |
|----------|------------|
| `gatewayUrl` | APIM root from step 3 (pre-filled if you used the generator) |
| `bearerToken` | Output of `az account get-access-token --resource api://coupon-service --query accessToken -o tsv` |
| `orderId` | Leave empty — set automatically by **POST place order** test script |
| `adminSubscriptionKey` | Only for folder **4 Admin** — from APIM portal → Subscriptions |

Collection **Authorization** is Bearer `{{bearerToken}}`. Health requests override with **No auth**.

### Run order (Collection Runner)

Run folders top to bottom:

| Folder | Auth | What it proves |
|--------|------|----------------|
| **1 Health** | None | Liveness and startup seed |
| **2 Coupons** | Bearer | SAVE10 preview applied; OLDCODE rejected (HTTP 200) |
| **3 Orders** | Bearer | Catalog; place order (saves `orderId`); get order |
| **4 Admin** | Bearer + subscription key | Optional — list policies, manifest |

### Regenerate collections

After APIM hostnames change:

```powershell
./scripts/generate-postman-collections.ps1
```

Writes both JSON files under `postman/`, auto-discovering gateway URLs when `az` is logged in.

---

## 7. Manual curl example

```powershell
$gateway = $devGateway
$token = az account get-access-token --resource api://coupon-service --query accessToken -o tsv

curl.exe -s "$gateway/coupons/v1/coupons/preview" `
  -H "Content-Type: application/json" `
  -H "Authorization: Bearer $token" `
  -d "{\"code\":\"SAVE10\",\"customerId\":\"test\",\"confirmedOrderCount\":0,\"cart\":{\"lines\":[{\"lineId\":\"1\",\"pizzaId\":\"margherita\",\"category\":\"classic\",\"unitPrice\":20,\"quantity\":2}]}}"
```

---

## 8. Seeded coupon codes

Loaded at startup in every deployed environment (`src/CouponService.Api/Seeding/SeedPolicies.json`):

| Code | Behaviour |
|------|-----------|
| `SAVE10` | 10% off |
| `FLAT5` | €5 off when subtotal ≥ €20 |
| `VEGGIE15` | 15% off vegetarian lines |
| `OLDCODE` | Rejected — expired |
| `LIMITED1` | Rejected after global cap |

---

## 9. Suggested first session (~10 minutes)

1. `./scripts/setup-entra-app.ps1` (if not done)
2. `az login` and discover `$devGateway`
3. `./scripts/test-deployed-apis.ps1 -GatewayUrl $devGateway -SkipOrder`
4. Acquire `$token`, paste into Postman **bearerToken**
5. Postman Collection Runner: folders **1 → 2**
6. Remove `-SkipOrder` and assign `Coupon.Redeem` if testing checkout
7. Repeat on `$prodGateway`

---

## 10. Troubleshooting

| Symptom | Likely cause |
|---------|----------------|
| **401** on preview | Wrong token (`aud` = management) or missing `Authorization` header |
| **404** on preview | URL missing `/coupons` prefix before `/v1/...` |
| Preview **200** + `Rejected` | Business rule (try `SAVE10` with quantity 2 × €20) |
| **400** on place order (`couponPolicy`) | Order API serializes `couponPolicy` as an integer enum (`0` = allow without discount, `1` = require discount) — omit the field to use the default |
| Place order **500** with `couponCode` | Order API MI token failed — assign `Coupon.Redeem`, then ensure `OrderApi__CouponServiceScope` is `api://coupon-service/.default` and the identity endpoint is called with `resource=` (not `scope=`; Container Apps rejects `scope`) |
| Place order fails (other) | `Coupon.Redeem` not assigned to Order API identity |
| Admin **401/403** | Missing `Coupon.Admin` and/or APIM admin subscription key |
| `consent_required` on token | Re-run `setup-entra-app.ps1` |
