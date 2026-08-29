# Deployment

First live Bicep apply into `rg-coupon-demo` (**CS-27**). Expand auth, APIM JWT and pipeline wiring in later tickets (**CS-28**–**CS-30**).

## Region note (observed)

Prefer a **single** region for the whole demo when the subscription allows it.

| Attempt | Result |
|---|---|
| `westeurope` | Rejected for new resources (`RequestDisallowedByAzure` / locationineligible) |
| `northeurope` | Cosmos `ServiceUnavailable` (capacity / AZ demand) |
| `eastasia` | Cosmos `ServiceUnavailable` (high demand); APIM name held by soft-delete |
| `eastus2` | **Succeeded** — Cosmos, APIM Consumption, Container Apps, ACR Basic, SWA Free all in one region |

Demo apply uses **`eastus2` for both `location` and `staticWebAppLocation`**. Template defaults in `main.bicep` remain `westeurope` for subscriptions that can use them.

Do not commit subscription IDs, tenant IDs or connection strings. Use `az account show` locally.

## Prerequisites

1. `az login` and `az account set` to the demo subscription.
2. Resource group `rg-coupon-demo` exists (empty is fine):

```powershell
az group create --name rg-coupon-demo --location eastus2
```

3. No portal configuration of individual resources — Bicep provisions them (AC-9.1).

## What-if then create (AC-9.2)

```powershell
az deployment group what-if `
  --resource-group rg-coupon-demo `
  --template-file infra/bicep/main.bicep `
  --parameters infra/bicep/main.demo.bicepparam

az deployment group create `
  --resource-group rg-coupon-demo `
  --template-file infra/bicep/main.bicep `
  --parameters infra/bicep/main.demo.bicepparam `
  --name cs27-first-provision
```

## First apply result (observed)

- `az deployment group what-if` against `rg-coupon-demo` / `eastus2`: exit `0` (Create set for the full module graph).
- `az deployment group create --name cs27-first-provision`: **`provisioningState=Succeeded`** (exit `0`).
- Iteration notes before green:
  - Key Vault: `enablePurgeProtection` must be `true` on this subscription.
  - APIM products: omit `approvalRequired` when `subscriptionRequired` is false; admin product keeps `subscriptionRequired: true` so `coupon-service` can sit in customer (open) and admin without “more than one open product”.
  - Name salt `uniqueString(resourceGroup().id, 'cs27')` avoids soft-deleted APIM global name clashes from earlier failed regions.

## Resource list (observed in `rg-coupon-demo` / `eastus2`)

| Resource | Name | SKU / notes (NFR-6) |
|---|---|---|
| Log Analytics | `law-coupon-demo` | PerGB2018, daily quota 1 GB |
| Application Insights | `appi-coupon-demo` | Workspace-based |
| Managed identities | `id-coupon-api-demo`, `id-order-api-demo` | User-assigned |
| Key Vault | `kv-coupon-demo-fqodzyjma` | Standard, RBAC, purge protection on |
| Cosmos DB | `cosmos-coupon-demo-fqodzyjmaar2g` | Serverless + free tier (`EnableServerless`, `enableFreeTier: true`) |
| Container Registry | `acrfqodzyjmaar2g` | **Basic** |
| Container Apps env | `cae-coupon-demo` | Consumption |
| Container Apps | `ca-coupon-api-demo`, `ca-order-api-demo` | P-11 placeholder image |
| API Management | `apim-coupon-demo-fqodzyjmaar2g` | **Consumption** (not Developer) |
| APIM APIs / products | coupon-service, order-service, customer (open), admin (subscription required) | JWT in CS-28 |
| Static Web App | `swa-coupon-demo` | Free |

## Placeholder image (P-11)

Both Container Apps start on `mcr.microsoft.com/k8se/quickstart:latest` so the first provision into an empty registry does not deadlock. Later pipeline image pushes update revisions (**CS-29**).

## Tear down

```powershell
az group delete --name rg-coupon-demo --yes --no-wait
```

Soft-deleted Key Vault may retain the name briefly; purge protection means the vault cannot be purged until the retention window ends.
