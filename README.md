# Coupon Service

Standalone coupon policy engine and redemption lifecycle for a pizza ordering platform. Coupons are rules as data; preview is advisory; checkout re-prices and reserves authoritatively through a thin Order API stand-in.

**Runtime:** .NET 10 · ASP.NET Core · Cosmos DB · Azure API Management · Container Apps · Bicep · Azure DevOps

## Documents

| Document | Purpose |
|---|---|
| [docs/solution-architecture.md](docs/solution-architecture.md) | As-built design: policy engine, redemption, APIM, auth, data, CI/CD, ADRs |
| [docs/deployment.md](docs/deployment.md) | Multi-stage CD (`develop` → non-prod, `main` → prod), SKUs, tear-down |
| [docs/authentication.md](docs/authentication.md) | Entra apps, APIM `validate-jwt`, managed-identity hop, local test tokens |
| [docs/pipeline-prerequisites.md](docs/pipeline-prerequisites.md) | One-time Azure DevOps / WIF / RBAC setup |
| [docs/testing-deployed-apis.md](docs/testing-deployed-apis.md) | Manual E2E testing: Postman collections, `test-deployed-apis.ps1`, tokens |
| [docs/assumptions.md](docs/assumptions.md) | Currency, region, SKUs, dual RGs, deferred work |
| [docs/api/README.md](docs/api/README.md) | Scalar / ReDoc / OpenAPI in the running APIs |
| [data/README.md](data/README.md) | Pizza catalog seed |

## Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download) (pinned in `global.json`)
- PowerShell 5.1+ (Windows) or pwsh for scripts
- Optional: Docker Desktop for Cosmos emulator integration tests (`docker compose up -d`)
- Optional: Azure CLI + Entra access to call the deployed APIM gateway

## Quick start (local)

Local hosts use **in-memory** policy and redemption stores. No Cosmos or Azure account is required for day-to-day API work.

```powershell
# Terminal 1 — Coupon Service (http://localhost:5174)
$env:Seeding__Enabled = "true"   # loads SAVE10, FLAT5, VEGGIE15, … at startup
dotnet run --project src/CouponService.Api

# Terminal 2 — Order API (http://localhost:5043), optional for checkout demos
dotnet run --project src/OrderApi
```

Interactive docs (Development only):

| Service | Scalar (try it) | ReDoc | OpenAPI |
|---|---|---|---|
| Coupon Service | http://localhost:5174/scalar | http://localhost:5174/redoc | http://localhost:5174/openapi/v1.json |
| Order API | http://localhost:5043/scalar | http://localhost:5043/redoc | http://localhost:5043/openapi/v1.json |

### Local auth (roles)

| Route | Auth on the app |
|---|---|
| `POST /v1/coupons/preview`, health | Open (APIM still requires JWT in Azure) |
| `GET /v1/pizzas`, orders | Open locally (JWT at APIM in Azure) |
| `POST /v1/reservations*` | `Coupon.Redeem` |
| Admin policies + engine manifest | `Coupon.Admin` |

For admin and reservation calls locally, enable the Development-only test-token scheme (see [docs/authentication.md](docs/authentication.md)):

```powershell
dotnet user-secrets set "Authentication:TestToken:Enabled" "true" --project src/CouponService.Api
```

Generate JWTs with issuer `coupon-service-test`, audience `api://coupon-service`, and the default signing key from `AuthenticationOptions` (`local-dev-only-symmetric-key-min-32-chars!`), or reuse the BDD `TokenProvider` pattern in `tests/CouponService.Bdd`. Never enable test tokens outside Development/Test — `TestTokenStartupGuard` throws at startup.

Point Order API at the Coupon Service when exercising checkout locally:

```powershell
dotnet user-secrets set "OrderApi:CouponServiceBaseUrl" "http://localhost:5174" --project src/OrderApi
dotnet user-secrets set "OrderApi:CouponServiceToken" "<redeem-test-jwt>" --project src/OrderApi
```

### Example: preview a seeded coupon

With seeding enabled:

```powershell
$body = @{
  code = "SAVE10"
  customerId = "demo-customer"
  confirmedOrderCount = 0
  cart = @{
    lines = @(
      @{
        lineId = "1"
        pizzaId = "margherita"
        category = "classic"
        unitPrice = 20.00
        quantity = 2
      }
    )
  }
} | ConvertTo-Json -Depth 6

Invoke-RestMethod -Method Post -Uri "http://localhost:5174/v1/coupons/preview" `
  -ContentType "application/json" -Body $body
```

A rejected coupon still returns **HTTP 200** with `status: "Rejected"` and a reason — that is intentional.

### Seeded demo codes

Loaded when `Seeding__Enabled=true` (always on in Azure). Source: `src/CouponService.Api/Seeding/SeedPolicies.json`.

| Code | Behaviour |
|---|---|
| `SAVE10` | 10% off |
| `FLAT5` | €5 off when subtotal ≥ €20 |
| `VEGGIE15` | 15% off vegetarian lines (capped), with extras |
| `BOGO` | Second item free-style (`nthItem`) |
| `EITHER` | Better of 15% or €5 |
| `OLDCODE` | Expired window → rejected with reason |
| `LIMITED1` | Global usage cap of 1 |
| *(no code)* | Automatic `TUESDAY10` on Tuesdays |

### Build and test

```powershell
dotnet build CouponService.slnx
dotnet test CouponService.slnx
```

Unit, engine, pricing and in-process BDD need **no** network or emulator. Cosmos integration tests need `docker compose up -d` first (see `docker-compose.yml`).

## Using the deployed APIs through APIM

Azure API Management is the **only public entry point**. For gateway URLs, bearer tokens, Postman collections, and the `test-deployed-apis.ps1` walkthrough, see **[docs/testing-deployed-apis.md](docs/testing-deployed-apis.md)**.

Quick reference — APIM path prefixes:

| Product | Gateway base | Backend |
|---|---|---|
| Customer (open product) | `{gateway}/coupons` | Coupon Service |
| Customer | `{gateway}/orders` | Order API |
| Admin (subscription required) | `{gateway}/admin` | Coupon Service |

### Public gateway endpoints

All customer routes (except health) require `Authorization: Bearer <Entra access token>` with audience `api://coupon-service` (or the app’s client id — both are accepted). Admin routes also require role `Coupon.Admin` and an APIM subscription key on the admin product.

| Method | Gateway URL | Purpose |
|---|---|---|
| `POST` | `{gateway}/coupons/v1/coupons/preview` | Advisory coupon evaluation |
| `GET` | `{gateway}/coupons/v1/health/live` | Liveness (anonymous) |
| `GET` | `{gateway}/coupons/v1/health/ready` | Readiness + startup seed (anonymous) |
| `GET` | `{gateway}/orders/v1/pizzas` | Catalog |
| `POST` | `{gateway}/orders/v1/orders` | Place order (server re-prices + reserves) |
| `GET` | `{gateway}/orders/v1/orders/{orderId}` | Fetch order |
| `GET`/`POST` | `{gateway}/admin/v1/admin/policies` | List / create policies |
| `GET`/`PUT`/`DELETE` | `{gateway}/admin/v1/admin/policies/{id}` | Read / update / archive |
| `GET` | `{gateway}/admin/v1/policy-engine/manifest` | Engine capability manifest |

**Not on APIM:** `POST /v1/reservations`, `…/confirm`, `…/release`. Those stay on the Coupon Service and are called **directly** by the Order API with managed identity + `Coupon.Redeem` (no browser path).

### Example: preview through the gateway

```powershell
$gateway = "https://apim-coupon-dev-….azure-api.net"   # your apimGatewayUrl
$token = az account get-access-token --resource api://coupon-service --query accessToken -o tsv

Invoke-RestMethod -Method Post `
  -Uri "$gateway/coupons/v1/coupons/preview" `
  -Headers @{ Authorization = "Bearer $token" } `
  -ContentType "application/json" `
  -Body $body
```

Anonymous preview must return **401** at the gateway. Full auth setup: [docs/authentication.md](docs/authentication.md). Post-deploy smoke: `scripts/smoke-deployed-stack.ps1`. Step-by-step manual testing: [docs/testing-deployed-apis.md](docs/testing-deployed-apis.md).

## How the pieces fit

```text
Caller (curl / SPA / pipeline)
        │  Bearer JWT
        ▼
   Azure APIM  ──►  Order API  ──MI + Coupon.Redeem──►  Coupon Service
        │                │                              (preview, admin,
        └──── preview ───┘                               reserve internal)
                              │
                              ▼
                         Cosmos DB (Azure)
                    or in-memory (local run)
```

1. **Preview** — client → APIM → Coupon Service. Read-only; never reserves.
2. **Checkout** — client → APIM → Order API → Coupon Service reservations (internal). Order total comes from the server, not the browser.
3. **Admin** — campaign manager → APIM admin product → policy CRUD + manifest.

## Repository layout

```text
src/CouponService.Api|Application|Domain|Engine|Infrastructure
src/OrderApi                 # Thin checkout stand-in
tests/                       # Unit, engine, API contract, BDD, integration
infra/bicep/                 # Delivery IaC (Terraform folder is documentation only)
data/                        # Pizza catalog snapshot
docs/                        # Architecture and delivery notes
scripts/                     # Entra setup, seed helper, deployed smoke
azure-pipelines.yml          # Sole CI/CD entry (P-13)
CouponService.slnx           # Solution (.NET 10 XML format)
```

## Git and environments

```text
feature/*  --PR (CI)-->  develop  --merge (CD)-->  rg-coupon-demo
develop    --PR (CI)-->  main     --merge (CD)-->  rg-coupon-prod
```

Details: [docs/deployment.md](docs/deployment.md). One-time pipeline setup: [docs/pipeline-prerequisites.md](docs/pipeline-prerequisites.md).

## Notes

- Money uses `decimal` only; round once to two places, `MidpointRounding.AwayFromZero`.
- Time in domain/engine/application comes from injected `IClock` — never `DateTime.UtcNow`.
- React SPA on Static Web Apps is **deferred**; APIs and BDD prove the contract without a UI.
- Do not commit secrets; use `dotnet user-secrets` and pipeline variable groups.
