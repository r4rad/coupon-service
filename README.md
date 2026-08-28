# Pizza order platform — coupons

Greenfield service: **there is no existing pizza ordering API to extend.** We build ordering (catalog + place order) and coupons in one new backend, with authenticated APIs on Azure API Management, automated deployment, BDD tests, and structured logging.

## Documents

| Document | Purpose |
|---|---|
| [docs/solution-architecture.md](docs/solution-architecture.md) | **The proposal.** Single diagram-led design document: the declarative policy engine (grammar, parsing, compilation, effects, governance), redemption lifecycle, data model, auth, scalability, performance, infrastructure, pipeline, tests, ADRs, risks. |
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
