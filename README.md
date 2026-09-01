# Pizza order platform — coupons

Greenfield service: **there is no existing pizza ordering API to extend.** We build ordering (catalog + place order) and coupons in one new backend, with authenticated APIs on Azure API Management, automated deployment, BDD tests, and structured logging.

## Documents

| Document | Purpose |
|---|---|
| [docs/solution-architecture.md](docs/solution-architecture.md) | **The proposal.** Single diagram-led design document: the declarative policy engine (grammar, parsing, compilation, effects, governance), redemption lifecycle, data model, auth, scalability, performance, infrastructure, pipeline, tests, ADRs, risks. |
| [docs/deployment.md](docs/deployment.md) | Multi-stage CD (`develop` → non-prod, `main` → prod), empty-RG provision, what-if, SKUs, tear-down, links to `azure-pipelines.yml` and Bicep param files. |
| [docs/authentication.md](docs/authentication.md) | Entra app registrations, app roles, APIM `validate-jwt`, managed-identity hop, local test-token guard. |
| [docs/pipeline-prerequisites.md](docs/pipeline-prerequisites.md) | One-time Azure DevOps setup: private project, WIF service connection, RBAC on both resource groups, pipeline variables. |
| [docs/assumptions.md](docs/assumptions.md) | Currency, region, SKUs, dual RGs, P-12 caching correction, deferred work (simulate, shadow, SPA). |
| [data/README.md](data/README.md) | Catalog seed (mock menu snapshot, loaded from git). |

**IaC:** **Bicep** is what we implement and deploy. Terraform is documented as an equivalent route (author experience) and is out of scope for the pipeline.

## Repository layout (target)

```text
docs/                 # Architecture and delivery notes
infra/bicep/          # Assignment infrastructure
infra/terraform/      # Optional Terraform mirror — not used in CI
src/                  # API and domain
tests/                # BDD and unit tests
```

## Status

Architecture documented. Implementation follows the in-scope items and delivery waves in `docs/solution-architecture.md`.
