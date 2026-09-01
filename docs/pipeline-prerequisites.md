# Pipeline prerequisites

Azure Pipelines is the only CI and CD system for this repository (decisions **P-13**, **P-14**). There is no GitHub Actions workflow. Standing assumptions (currency, region, SKUs, deferred work) are in [`docs/assumptions.md`](assumptions.md).

One-time manual steps below are required before the first CD run. **Nothing else is manual** — after these exist, `azure-pipelines.yml` provisions, deploys, seeds, smokes and verifies without portal clicks.

## Branching model (P-14)

| Event | Pipeline |
|---|---|
| PR into `develop` or `main` | **CI only:** Build (restore, build, `az bicep build` + lint) and **Test**. No provision or deploy. |
| Merge to `develop` (or Manual targeting non-prod) | Full eight stages → **non-prod** (`rg-coupon-demo`, `main.dev.bicepparam`) |
| Merge to `main` (or Manual targeting prod) | Full eight stages → **production** (`rg-coupon-prod`, `main.prod.bicepparam`) |

```text
feature  --PR CI-->  develop  --merge CD-->  rg-coupon-demo (dev/demo)
develop  --PR CI-->  main     --merge CD-->  rg-coupon-prod
```

Protect `develop` and `main` so merges require a PR with the pipeline succeeding.

## 1. Azure DevOps project

Create (or reuse) a **private** Azure DevOps project (new orgs cannot enable public projects).

- Grant the pipeline identity rights to queue builds and read the repository.
- Create a pipeline that points at `azure-pipelines.yml` at the repository root.
- One YAML serves PR validation and both CD environments.

## 2. Workload-identity federated service connection

Create an Azure Resource Manager service connection that authenticates with **workload identity federation** (OIDC). Do **not** use a client secret or certificate password.

- Scope the connection to the subscription that contains the coupon RGs (preferred), or assign it on each RG.
- Name it to match the pipeline parameter default (`coupon-demo-wif`), or override `azureServiceConnection` when queuing.
- The YAML references the connection by name only (`azureSubscription: $(azureServiceConnection)`). No secret value appears in the YAML (acceptance criterion **AC-9.3**).

### RBAC the pipeline identity must have

Bicep assigns **Key Vault Secrets User** to app managed identities (`Microsoft.Authorization/roleAssignments/write`) and **AcrPull** so Container Apps can pull pipeline images. **Contributor alone is not enough.**

On **each** target resource group (`rg-coupon-demo` and `rg-coupon-prod`), grant the WIF service principal:

1. **Contributor**
2. **User Access Administrator** (or Owner)
3. **AcrPush** on each environment's ACR (or Contributor does not cover registry data-plane push — assign `AcrPush` after the first provision creates the registry)

Example (replace object id with the SP from the service connection):

```powershell
$spObjectId = '24fa000f-3b0a-4ae1-84f2-70e803aca6e0'
$sub = 'd2cb4e9e-8675-44eb-9667-45428711839a'
foreach ($rg in @('rg-coupon-demo', 'rg-coupon-prod')) {
  az role assignment create --assignee-object-id $spObjectId --assignee-principal-type ServicePrincipal `
    --role 'Contributor' --scope "/subscriptions/$sub/resourceGroups/$rg"
  az role assignment create --assignee-object-id $spObjectId --assignee-principal-type ServicePrincipal `
    --role 'User Access Administrator' --scope "/subscriptions/$sub/resourceGroups/$rg"
}
# After first provision, grant AcrPush on each registry (names from deployment outputs):
# az role assignment create --assignee-object-id $spObjectId --assignee-principal-type ServicePrincipal `
#   --role AcrPush --scope $(az acr show -n <acrName> -g <rg> --query id -o tsv)
```

## 3. Resource groups

Templates never create the resource group. Create them once:

```powershell
az group create --name rg-coupon-demo --location eastus2
az group create --name rg-coupon-prod --location eastus2
```

This subscription rejects `westeurope` for new resources (`RequestDisallowedByAzure` / locationineligible). Demo/dev/prod param files and the pipeline `location` default must be **`eastus2`** (see `docs/deployment.md`). Template defaults in `main.bicep` may still say `westeurope` for other subscriptions; the param file wins.

### Container Apps environment quota

Observed on this subscription (**not** merely one-per-region):

| Limit | Observed error | Effect |
|---|---|---|
| **At most one** Container Apps managed environment **in the whole subscription** | `MaxNumberOfGlobalEnvironmentsInSubExceeded` | Dev and prod cannot each create a CAE |
| Zero App Service VMs | `SubscriptionIsOverQuotaForSku` | The App Service F1 fallback cannot run here |

Develop CD creates the single CAE (`cae-coupon-dev` in `rg-coupon-demo`). **`main.prod.bicepparam` reuses it** via `existingManagedEnvironmentResourceGroup` / `existingManagedEnvironmentName`, so prod Container Apps in `rg-coupon-prod` attach to that environment instead of creating `cae-coupon-prod`. Run develop CD at least once before main CD. To give prod its own CAE after a quota increase, clear those two params and optionally set `containerAppsLocation` to a second region.
## 4. Entra app registration

The Coupon Service API app registration must exist before CD Seed, APIM `validate-jwt` (**AC-7.6**) or the Order API managed-identity hop (**AC-7.7**) can work. Without it, `az account get-access-token --resource api://coupon-service` fails with `AADSTS500011: The resource principal ... was not found in the tenant`, and Seed falls through to the `AdminApiBearerToken` fallback and returns **401**.

Run [`scripts/setup-entra-app.ps1`](../scripts/setup-entra-app.ps1) once per tenant, as an identity that may create app registrations and write `appRoleAssignedTo`. It is idempotent, so re-running it converges rather than duplicating:

```powershell
# Object id (not app id) of the service principal behind the coupon-demo-wif connection:
$wifSp = az ad sp show --id <service-connection-appId> --query id -o tsv
./scripts/setup-entra-app.ps1 -AdminPrincipalId $wifSp -DryRun
./scripts/setup-entra-app.ps1 -AdminPrincipalId $wifSp
```

It creates the application with identifier URI `api://coupon-service`, app roles `Coupon.Admin` and `Coupon.Redeem`, the resource service principal, and the `Coupon.Admin` assignment CD Seed needs. Pass `-RedeemPrincipalId` with the Order API managed identity's object id after the first provision to complete **AC-7.7**.

Two details the script exists to get right:

- **`requestedAccessTokenVersion` must be `2`.** `main.bicep` sets `jwtIssuer = jwtAuthority = https://login.microsoftonline.com/{tenantId}/v2.0`, and `JwtBearer` matches that issuer exactly. A registration left at the default version 1 issues tokens with `iss = https://sts.windows.net/{tenantId}/`, which fails validation as an indistinguishable 401.
- **`Coupon.Admin` must allow member type `Application`.** A Users/Groups-only role cannot be assigned to a service principal, so the client-credentials token would carry no `roles` claim and the request would be rejected with 403.

Then copy the printed client id into **`param couponApiClientId`** in `main.dev.bicepparam` and `main.prod.bicepparam`. Version 2 tokens carry that GUID in `aud` instead of the Application ID URI, so the API and the APIM edge both need it as a valid audience — see [`docs/authentication.md`](authentication.md#two-accepted-audiences-and-why). Skipping this step produces a 401 with a token that otherwise looks correct.

If your tenant restricts identifier URIs to the `api://{appId}` form, re-run with `-Audience api://<appId>` and set `couponApiAudience` to the same value in `main.dev.bicepparam` / `main.prod.bicepparam`.

Verify before re-running CD:

```powershell
az account get-access-token --resource api://coupon-service --query accessToken -o tsv
```

## 5. Globally unique names (Key Vault / APIM)

Key Vault names are globally unique and limited to 24 characters in this template (`take(..., 24)`). Soft-delete with **purge protection** means a deleted vault **cannot** be purged until the retention window ends — `VaultAlreadyExists` will keep failing for the same name.

- Do **not** append a salt to `uniqueSuffix` and expect the vault name to change: truncation drops a trailing salt.
- Put any collision salt at the **start** of `uniqueSuffix` (see CS-29). Prefer that over waiting for soft-delete expiry.

List soft-deleted vaults (diagnostic only):

```powershell
az keyvault list-deleted --query "[?contains(name, 'coupon')].{name:name, deletionDate:properties.deletionDate, purgeProtection:properties.purgeProtectionEnabled}" -o table
```

## Pipeline variables (not secrets in YAML)

Set these on the pipeline (or a variable group). Values are not committed:

| Variable | Secret? | Purpose |
|---|---|---|
| `AdminApiBearerToken` | yes | **No longer used by CD.** Seeding moved into the application (below), so the pipeline holds no admin credential. Keep it only if you drive `scripts/seed-policies.ps1` manually. |
| `AdminApiBaseUrl` | no | Optional override for the Coupon Service **backend** base URL used by seed verification. Default is `couponBackendUrl` from provision outputs. |
| `OrderApiBaseUrl` | no | Optional override for the Order API **backend** base URL. Stage 8 uses it; stage 7 uses `SmokeOrderBaseUrl`. |
| `SmokeCouponBaseUrl` | no | Optional override for the gateway URL stage 7 smokes. Default is `apimGatewayUrl` + `/coupons` from provision outputs. |
| `SmokeOrderBaseUrl` | no | Optional override for the Order API gateway URL stage 7 smokes. Default is `apimGatewayUrl` + `/orders`. |
| `CouponApiAudience` | no | Optional override for the token audience stage 7 requests. Default is `couponApiAudience` from provision outputs. |

### The pipeline holds no admin credential

CD used to acquire an admin JWT and POST the policy set through `/v1/admin/policies`. That coupled every deployment to Entra app registrations, app-role assignments and token-version details, and it wrote into a **per-instance** policy store — the service registers `InMemoryPolicyRepository`, so anything seeded over HTTP reached one replica and vanished on restart.

The Coupon Service now seeds itself as it starts (`Seeding__Enabled`, Bicep param `seedPoliciesOnStartup`), reading the same deterministic set from `src/CouponService.Api/Seeding/SeedPolicies.json`. Every replica converges to the same policies on every start, which is what **AC-9.5** and **AC-9.6** ask for.

The CD Seed stage therefore only **verifies**: it polls the anonymous readiness probe `/v1/health/ready` until the `policy-seed` health check reports healthy. No token, no gateway hop, nothing to expire.

`scripts/setup-entra-app.ps1` and the app roles are still required for **AC-7.6** and **AC-7.7** — human admins calling `/v1/admin/policies`, APIM `validate-jwt`, and the Order API managed-identity hop — but they are no longer on the deployment critical path.

Stage 7 does need the app registration to exist, because it requests a token for `couponApiAudience` to prove an authenticated preview works through the gateway. It needs **no app role**: the service authorizes only reservations (`Coupon.Redeem`) and admin (`Coupon.Admin`), and APIM's customer product checks audience and issuer without inspecting role claims. If the registration is missing, the stage fails with the `setup-entra-app.ps1` hint rather than degrading silently.

Branch → RG / param file mapping lives in `azure-pipelines.yml` after CS-29 so Manual runs can still override parameters if needed.

## What the pipeline does after this

| Trigger | Stages |
|---|---|
| Pull request → `develop` or `main` | **Build** + **Test** only |
| Push/merge `develop` | All eight stages → non-prod RG |
| Push/merge `main` | All eight stages → prod RG |
| Manual | All eight stages; pick RG/params via pipeline parameters |

A green multi-environment CD path is **CS-29**. This document lists the one-time human prerequisites that cannot be expressed in YAML alone.
