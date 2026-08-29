# Pipeline prerequisites

Azure Pipelines is the only CI and CD system for this repository (decision **P-13**). There is no GitHub Actions workflow.

Three one-time manual steps are required before the first CD run. **Nothing else is manual** — after these exist, `azure-pipelines.yml` provisions, deploys, seeds, runs BDD and verifies without portal clicks.

## 1. Azure DevOps project

Create (or reuse) an Azure DevOps project that will hold the repository and the pipeline.

- Grant the pipeline identity rights to queue builds and read the repository.
- Create a pipeline that points at `azure-pipelines.yml` at the repository root.
- PR validation and the `main` branch continuous delivery path both use that single YAML file.

## 2. Workload-identity federated service connection

Create an Azure Resource Manager service connection that authenticates with **workload identity federation** (OIDC). Do **not** use a client secret or certificate password.

- Scope the connection to the subscription that contains `rg-coupon-demo`, or to the `rg-coupon-demo` resource group itself.
- Name it to match the pipeline parameter default (`coupon-demo-wif`), or override the `azureServiceConnection` parameter when queuing.
- The YAML references the connection by name only (`azureSubscription: $(azureServiceConnection)`). No secret value appears in the YAML or in a variable group (acceptance criterion **AC-9.3**).

The resource group `rg-coupon-demo` must already exist (empty is fine). Bicep targets the resource group supplied by the deployment command; the template never creates the group and never bakes in the name.

## 3. Entra app registration permission

Grant permission to create or configure the Entra app registrations the demo needs (workforce / External ID apps used for JWT validation and managed-identity role assignment). Wave 8 (**CS-28**) applies those registrations; the pipeline and Bicep assume the operator who wired the service connection can also complete that Entra work when requested.

## Pipeline variables (not secrets in YAML)

Set these on the pipeline (or a non-secret / secret variable group as appropriate). Values are not committed:

| Variable | Secret? | Purpose |
|---|---|---|
| `AdminApiBearerToken` | yes | Admin-role bearer used by `scripts/seed-policies.ps1` |
| `AdminApiBaseUrl` | no | Optional override for the coupon admin base URL (otherwise taken from provision outputs / APIM) |
| `OrderApiBaseUrl` | no | Optional override for the Order API base URL used by post-deploy BDD |

## What the pipeline does after this

| Trigger | Stages |
|---|---|
| Pull request | **Build** (restore, build, `az bicep build` + lint) and **Test**. No provision or deploy. |
| `main` or Manual | All eight stages: Build → Test → Package → Provision (what-if artifact, then create) → Deploy → Seed → BDD → Verify. |

A green full CD run against the live subscription is **CS-29**. This document only lists the one-time human prerequisites that cannot be expressed in YAML.
