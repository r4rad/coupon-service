# Deployment

Live apply and multi-stage CD (**CS-27**, **CS-29**, **CS-30**). Auth, APIM JWT and Entra details live in [`docs/authentication.md`](authentication.md). Pipeline one-time setup lives in [`docs/pipeline-prerequisites.md`](pipeline-prerequisites.md). Standing assumptions (currency, SKUs, deferred work) live in [`docs/assumptions.md`](assumptions.md).

**Pipeline definition:** [`azure-pipelines.yml`](../azure-pipelines.yml) at the repository root — the sole CI/CD entry point (P-13). **Parameter files:** [`infra/bicep/main.dev.bicepparam`](../infra/bicep/main.dev.bicepparam) (develop / non-prod), [`infra/bicep/main.prod.bicepparam`](../infra/bicep/main.prod.bicepparam) (main / production), [`infra/bicep/main.demo.bicepparam`](../infra/bicep/main.demo.bicepparam) (CS-27 manual apply only).

## Git workflow (P-14)

```text
feature/ticket-CS-XX  --PR (CI)-->  develop  --merge (CD)-->  rg-coupon-demo
develop               --PR (CI)-->  main     --merge (CD)-->  rg-coupon-prod
```

Ticket branches are created from the latest `develop` and open pull requests against `develop`. The operator merges `develop` → `main` separately after the non-prod eight-stage CD run is green — not as part of a feature pull request.

## Branch → environment

| Branch / event | Resource group | Param file | Notes |
|---|---|---|---|
| PR into `develop` or `main` | — | — | CI only (Build + Test + bicep lint). No provision. |
| Merge to `develop` | `rg-coupon-demo` | `infra/bicep/main.dev.bicepparam` | Non-prod; `environmentName=dev`, `hostingMode=containerApps` |
| Merge to `main` | `rg-coupon-prod` | `infra/bicep/main.prod.bicepparam` | Production; `environmentName=prod`; CAE in `eastus` (`containerAppsLocation`), every other resource in `eastus2` |

### Empty resource group → green pipeline (AC-9.1)

After [`docs/pipeline-prerequisites.md`](pipeline-prerequisites.md) is satisfied, a merge to `develop` or `main` runs the same eight stages in [`azure-pipelines.yml`](../azure-pipelines.yml): **Build** → **Test** → **Package** → **Provision** (what-if artifact, then apply) → **Deploy** (ACR images, Container App update, readiness probe) → **Seed** (idempotent admin API) → **BDD** (Reqnroll through APIM) → **Verify**. No portal configuration of individual resources is required between stages.

The WIF service principal needs **Contributor** and **User Access Administrator** on both RGs (Key Vault `roleAssignments/write`), plus **AcrPush** on each ACR after first provision. See `docs/pipeline-prerequisites.md`.

## Region note (observed)

Prefer a **single** region for the whole demo when the subscription allows it.

| Attempt | Result |
|---|---|
| `westeurope` | Rejected for new resources (`RequestDisallowedByAzure` / locationineligible) |
| `northeurope` | Cosmos `ServiceUnavailable` (capacity / AZ demand) |
| `eastasia` | Cosmos `ServiceUnavailable` (high demand); APIM name held by soft-delete |
| `eastus2` | **Succeeded** — Cosmos, APIM Consumption, Container Apps, ACR Basic, SWA Free |

This subscription allows **at most one Container Apps environment in the whole subscription** (`MaxNumberOfGlobalEnvironmentsInSubExceeded` — stricter than one-per-region) and **zero App Service VMs**, so production cannot raise a second CAE while non-prod holds `cae-coupon-dev`. Param files still declare `hostingMode = containerApps` with prod `containerAppsLocation = 'eastus'` so a quota increase unlocks prod without rewriting the template. Until then, prove the develop CD path only.

Demo/dev/prod param files and the pipeline `location` default use **`eastus2`**. Template defaults in `main.bicep` remain `westeurope` for subscriptions that can use them.

Do not commit subscription IDs, tenant IDs or connection strings. Use `az account show` locally.

## Key Vault naming (leading salt)

Vault names use `take(..., 24)`. A salt appended to `uniqueSuffix` is truncated and does **not** change the name (soft-deleted `kv-coupon-demo-r4hxkv774` collided forever under purge protection). Put collision salts at the **start** of `uniqueSuffix`, e.g. `take('v29${uniqueString(resourceGroup().id)}', 13)`. Do not rely on purge when `enablePurgeProtection` is true.

## Prerequisites

1. `az login` and `az account set` to the demo subscription.
2. Resource groups exist (empty is fine):

```powershell
az group create --name rg-coupon-demo --location eastus2
az group create --name rg-coupon-prod --location eastus2
```

3. No portal configuration of individual resources — Bicep provisions them (AC-9.1).

## What-if then create (AC-9.2)

Non-prod (matches develop CD):

```powershell
az deployment group what-if `
  --resource-group rg-coupon-demo `
  --template-file infra/bicep/main.bicep `
  --parameters infra/bicep/main.dev.bicepparam

az deployment group create `
  --resource-group rg-coupon-demo `
  --template-file infra/bicep/main.bicep `
  --parameters infra/bicep/main.dev.bicepparam `
  --name cs29-dev-provision
```

`main.demo.bicepparam` remains the CS-27 manual-apply file (`environmentName=demo`). Do not run it against `rg-coupon-demo` while the develop pipeline owns that RG — it competes for the single eastus2 CAE slot.

## First apply result (observed)

- `az deployment group what-if` against `rg-coupon-demo` / `eastus2`: exit `0` (Create set for the full module graph).
- `az deployment group create --name cs27-first-provision`: **`provisioningState=Succeeded`** (exit `0`).
- CS-29 develop path (`main.dev.bicepparam`, leading `v29` salt): Container Apps environment `cae-coupon-dev` and apps created; Key Vault `kv-coupon-dev-v29r4hxkv7`.
- Iteration notes before green:
  - Key Vault: `enablePurgeProtection` must be `true` on this subscription.
  - APIM products: omit `approvalRequired` when `subscriptionRequired` is false; admin product keeps `subscriptionRequired: true` so `coupon-service` can sit in customer (open) and admin without “more than one open product”.
  - Leading name salt (above) avoids soft-deleted Key Vault global name clashes; trailing salts are dropped by `take(..., 24)`.

## Resource list (observed in `rg-coupon-demo` / `eastus2`, develop / `environmentName=dev`)

| Resource | Name | SKU / notes (NFR-6) |
|---|---|---|
| Log Analytics | `law-coupon-dev` | PerGB2018, daily quota 1 GB |
| Application Insights | `appi-coupon-dev` | Workspace-based |
| Managed identities | `id-coupon-api-dev`, `id-order-api-dev` | User-assigned; AcrPull on ACR |
| Key Vault | `kv-coupon-dev-v29r4hxkv7` | Standard, RBAC, purge protection on |
| Cosmos DB | `cosmos-coupon-dev-v29r4hxkv774j` | Serverless (`EnableServerless`; free tier off when allotment used) |
| Container Registry | `acrv29r4hxkv774j` | **Basic** |
| Container Apps env | `cae-coupon-dev` | Consumption |
| Container Apps | `ca-coupon-api-dev`, `ca-order-api-dev` | P-11 placeholder then pipeline image |
| API Management | `apim-coupon-dev-v29r4hxkv774j` | **Consumption** (not Developer) |
| APIM APIs / products | coupon-service, order-service, customer (open), admin (subscription required) | JWT in CS-28 |
| Static Web App | `swa-coupon-dev` | Free |

## Placeholder image then pipeline images (P-11 / CS-29)

Both Container Apps start on `mcr.microsoft.com/k8se/quickstart:latest` so the first provision into an empty registry does not deadlock. The Deploy stage builds `src/CouponService.Api/Dockerfile` and `src/OrderApi/Dockerfile` on the agent (`docker build` / `docker push` after `az acr login`), updates the Container App revisions, then probes `/v1/health/ready` before Seed. This subscription rejects ACR Tasks (`TasksOperationsNotAllowed`), so the pipeline must not use `az acr build`.

Post-deploy BDD (stage 7) targets the APIM gateway URLs (`…/coupons`, `…/orders`) from provision outputs unless `AdminApiBaseUrl` / `OrderApiBaseUrl` override them.

## Tear down

```powershell
az group delete --name rg-coupon-demo --yes --no-wait
az group delete --name rg-coupon-prod --yes --no-wait
```

Soft-deleted Key Vault may retain the name; purge protection means the vault cannot be purged until the retention window ends — change the leading `uniqueSuffix` salt instead of waiting.
