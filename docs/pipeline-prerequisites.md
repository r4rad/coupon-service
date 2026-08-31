# Pipeline prerequisites

Azure Pipelines is the only CI and CD system for this repository (decisions **P-13**, **P-14**). There is no GitHub Actions workflow.

One-time manual steps below are required before the first CD run. **Nothing else is manual** — after these exist, `azure-pipelines.yml` provisions, deploys, seeds, runs BDD and verifies without portal clicks.

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
| **At most one** Container Apps managed environment **in the whole subscription** | `MaxNumberOfGlobalEnvironmentsInSubExceeded` | Dev and prod cannot both host Container Apps at once |
| Zero App Service VMs | `SubscriptionIsOverQuotaForSku` | The App Service F1 fallback cannot run here |

`main.dev.bicepparam` / develop CD owns the single CAE slot (`cae-coupon-dev` in eastus2). `main.prod.bicepparam` still sets `hostingMode = containerApps` and `containerAppsLocation = 'eastus'` (P-14) so a quota increase or a second subscription unlocks production without another template change — until then, **prod provision fails** while the non-prod CAE exists. `main.demo.bicepparam` also targets `containerApps` (`cae-coupon-demo`) — do not run it against `rg-coupon-demo`, or it fights the develop pipeline for the same global slot.
## 4. Entra app registration permission

Grant permission to create or configure the Entra app registrations the demo needs (JWT validation and managed-identity role assignment). Wave 8 (**CS-28**) applies those registrations; the operator who wired the service connection completes that Entra work when requested.

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
| `AdminApiBearerToken` | yes | Admin-role bearer used by `scripts/seed-policies.ps1` |
| `AdminApiBaseUrl` | no | Optional override for the coupon admin base URL (otherwise provision outputs / APIM) |
| `OrderApiBaseUrl` | no | Optional override for Order API base URL used by post-deploy BDD |

Branch → RG / param file mapping lives in `azure-pipelines.yml` after CS-29 so Manual runs can still override parameters if needed.

## What the pipeline does after this

| Trigger | Stages |
|---|---|
| Pull request → `develop` or `main` | **Build** + **Test** only |
| Push/merge `develop` | All eight stages → non-prod RG |
| Push/merge `main` | All eight stages → prod RG |
| Manual | All eight stages; pick RG/params via pipeline parameters |

A green multi-environment CD path is **CS-29**. This document lists the one-time human prerequisites that cannot be expressed in YAML alone.
