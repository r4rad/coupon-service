# Assumptions

Standing decisions and constraints that are not repeated in every ticket. Authoritative acceptance criteria remain in `.kiro/specs/coupon-service/requirements.md`; planning decisions **P-1**…**P-14** live in `.kiro/specs/coupon-service/design.md`.

## Currency and money

- All monetary values use `decimal`, never `double` or `float`.
- Rounding happens once when a monetary value is produced: two decimal places, `MidpointRounding.AwayFromZero`.
- Catalog currency in `data/pizzas.json` and API responses is **EUR** for the demo.

## Region and subscription

- Live deployment region for this subscription is **`eastus2`** (`westeurope` was rejected as location-ineligible).
- Static Web Apps and most resources stay in `eastus2`. Production Container Apps reuse the develop CD environment (`cae-coupon-dev` in `rg-coupon-demo`) because this subscription allows only one CAE globally.
- Both develop and main CD paths are intended to run green on this subscription when develop CD has provisioned the shared CAE first. See `docs/deployment.md`.

## SKUs (NFR-6 demo posture)

| Resource | Tier | Notes |
|---|---|---|
| API Management | **Consumption** | Not Developer; first 1M calls/month included |
| Container Apps | Consumption | Scale to zero; placeholder image on first provision (P-11) |
| Container Registry | **Basic** | Only recurring non-free item (~USD 5/month) |
| Cosmos DB | Serverless | Free tier used if available; otherwise serverless |
| Static Web Apps | Free | SPA shell + CDN |
| Key Vault | Standard | RBAC, purge protection on this subscription |
| Application Insights + Log Analytics | Pay as you go | Short retention, daily cap |

## Azure DevOps and pipeline (P-13, P-14)

- **Azure Pipelines only** — one `azure-pipelines.yml`; no GitHub Actions workflow.
- The Azure DevOps project is **private** (new organisations cannot enable public projects).
- **Branching:** feature → `develop` (PR runs CI) → merge triggers CD to `rg-coupon-demo`; `develop` → `main` (PR runs CI) → merge triggers CD to `rg-coupon-prod`. Same eight stages; branch selects resource group and `bicepparam` file.
- Pipeline authentication uses **workload identity federation** (OIDC) — no stored client secret (AC-9.3).
- The WIF service principal needs **Contributor** and **User Access Administrator** on **both** resource groups, plus **AcrPush** on each registry after first provision.

## Dual resource groups

| Resource group | Branch | Param file | `environmentName` |
|---|---|---|---|
| `rg-coupon-demo` | `develop` | `infra/bicep/main.dev.bicepparam` | `dev` |
| `rg-coupon-prod` | `main` | `infra/bicep/main.prod.bicepparam` | `prod` |

`main.demo.bicepparam` remains the CS-27 manual-apply file (`environmentName=demo`). Do not run it against `rg-coupon-demo` while the develop pipeline owns that group.

## Caching (P-12)

APIM **Consumption** has no internal response cache (`cache-lookup` would need external Redis). Catalog caching relies on **Static Web Apps CDN** for the SPA bundle and on the Order API returning **`ETag`** and **`Cache-Control`** on `GET /v1/pizzas` so clients and intermediaries can serve conditional requests without hitting the backend on every navigation.

## Deferred work (not in scope for Wave 8)

| Item | Acceptance criteria | Status |
|---|---|---|
| `simulate` / shadow evaluation endpoints | AC-6.5, AC-6.6 | Deferred — manifest types exist; HTTP surface not shipped |
| Alerts and workbook | AC-8.5 | Deferred — Log Analytics queries documented; alert rules not provisioned |
| React SPA on Static Web Apps | Optional stretch | Deferred — APIs and BDD prove the contract; no SPA in repo |
