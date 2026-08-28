# Terraform (documented route — out of scope for this assignment)

This folder is a **route**, not the delivery path.

The author has worked with Terraform (`azurerm`, plan/apply, remote state). For **this** assignment we use **Bicep** so the pipeline stays native to Azure DevOps and there is no Terraform state backend to create or secure.

If this stack were ported:

- Same resources and SKUs as `infra/bicep` (F1, APIM Consumption, Insights).
- State in Azure Storage (or Terraform Cloud) — that backend is **out of scope** here.
- CI would run `terraform plan` / `apply` instead of `az deployment`.

Do not apply Terraform in parallel with Bicep against the same resource group. Pick one tool per environment.
