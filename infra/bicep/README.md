# Bicep (assignment — in scope)

Provisions the full coupon-service demo into an existing resource group. Correctness for authorship is `az bicep build` and `az bicep lint`; the first live apply is a later ticket.

```text
infra/bicep/
  main.bicep · main.demo.bicepparam
  modules/
    observability · identity · keyvault · cosmos · acr
    containerapps · appservice (fallback) · apim · apim-api · staticwebapp
```

- Default location: `westeurope`. The resource group name is not baked into the template.
- SKUs follow section 17 / NFR-6: APIM Consumption, Container Apps consumption, Static Web Apps Free, Cosmos serverless with a free-tier switch, Log Analytics with a daily cap, ACR Basic as the only paid SKU. App Service F1 is the optional fallback via `hostingMode`.
- Container Apps start on a public placeholder image (P-11) so the first deploy into an empty registry does not deadlock.
- Every resource is tagged `project`, `env`, `owner`.
- `main.demo.bicepparam` carries no secrets.

What-if then create (not run in the authorship ticket):

```powershell
az deployment group what-if --resource-group <rg> --template-file infra/bicep/main.bicep --parameters infra/bicep/main.demo.bicepparam
az deployment group create --resource-group <rg> --template-file infra/bicep/main.bicep --parameters infra/bicep/main.demo.bicepparam
```

See [docs/solution-architecture.md](../../docs/solution-architecture.md) section 17.
