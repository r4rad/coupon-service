# Bicep (assignment — in scope)

Author the Azure resources for the coupon-service demo. **This ticket's correctness bar is `az bicep build` and `az bicep lint` only.** The first live apply (`az deployment group what-if` / `create`) is **CS-27**.

```text
infra/bicep/
  main.bicep · main.demo.bicepparam
  modules/
    observability · identity · keyvault · cosmos · acr
    containerapps · appservice (fallback) · apim · apim-api · staticwebapp
```

- Default location: `westeurope`. The resource group name is not baked into the template; the deployment command supplies it.
- SKUs follow section 17 / NFR-6: APIM Consumption, Container Apps consumption, Static Web Apps Free, Cosmos serverless with a free-tier switch, Log Analytics with a daily cap, ACR Basic as the only paid SKU. App Service F1 is the optional fallback via `hostingMode`.
- Container Apps start on a public placeholder image (P-11) so the first deploy into an empty registry does not deadlock.
- Every resource is tagged `project`, `env`, `owner`.
- `main.demo.bicepparam` carries no secrets.

Verify authorship (CS-25):

```powershell
az bicep build --file infra/bicep/main.bicep --stdout
az bicep lint --file infra/bicep/main.bicep
```

First live apply (CS-27 — not this ticket):

```powershell
az deployment group what-if --resource-group <rg> --template-file infra/bicep/main.bicep --parameters infra/bicep/main.demo.bicepparam
az deployment group create --resource-group <rg> --template-file infra/bicep/main.bicep --parameters infra/bicep/main.demo.bicepparam
```

CI/CD for these templates is Azure Pipelines only (P-13 / CS-26). No GitHub Actions.

See [docs/solution-architecture.md](../../docs/solution-architecture.md) section 17.
