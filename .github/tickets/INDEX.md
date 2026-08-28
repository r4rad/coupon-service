# Ticket index

26 tickets. Standing rules for every one of them live in
[`AGENTS.md`](../../AGENTS.md); the acceptance criteria they reference live in
[`.kiro/specs/coupon-service/requirements.md`](../../.kiro/specs/coupon-service/requirements.md).

Each ticket file ends with a **Prompt** section. Open the ticket, copy that block, paste it into
a fresh Cursor chat in this repository. There is nothing to install and no wrapper to run.

| Ticket | Title | Wave | Size | Blocked by |
|---|---|---|---|---|
| [CS-01](CS-01-solution-wiring-central-packages-and-build-gates.md) | Solution wiring, central packages and build gates | 0 | M | — |
| [CS-02](CS-02-domain-primitives-and-the-money-contract.md) | Domain primitives and the money contract | 0 | M | CS-01 |
| [CS-03](CS-03-value-model-and-expression-ast.md) | Value model and expression AST | 1 | S | CS-01 |
| [CS-04](CS-04-policy-parser-with-depth-and-node-budgets.md) | Policy parser with depth and node budgets | 1 | L | CS-03 |
| [CS-05](CS-05-fact-registry-fact-vocabulary-and-engine-manifest.md) | Fact registry, fact vocabulary and engine manifest | 1 | M | CS-03 |
| [CS-06](CS-06-policy-validator-with-full-error-reporting.md) | Policy validator with full error reporting | 1 | M | CS-04, CS-05 |
| [CS-07](CS-07-compiler-with-cost-ordering-and-the-evaluation.md) | Compiler with cost ordering, and the evaluation scope | 1 | L | CS-04, CS-05 |
| [CS-08](CS-08-content-hashing-and-the-bounded-compiled-policy.md) | Content hashing and the bounded compiled-policy cache | 1 | S | CS-07 |
| [CS-09](CS-09-core-effect-handlers-and-the-price-calculator.md) | Core effect handlers and the price calculator | 2 | L | CS-02, CS-07 |
| [CS-10](CS-10-advanced-effect-handlers-cheapestfree-nthitem.md) | Advanced effect handlers: cheapestFree, nthItem, tiered | 2 | M | CS-09 |
| [CS-11](CS-11-property-based-invariant-tests-for-pricing.md) | Property-based invariant tests for pricing | 2 | S | CS-10 |
| [CS-12](CS-12-application-services-and-in-memory-repositories.md) | Application services and in-memory repositories | 3 | M | CS-08, CS-09 |
| [CS-13](CS-13-redemption-lifecycle-reserve-confirm-release.md) | Redemption lifecycle: reserve, confirm, release, expire | 3 | L | CS-12 |
| [CS-14](CS-14-automatic-policies-storage-key-cached-index.md) | Automatic policies: storage key, cached index and resolution | 3 | M | CS-12 |
| [CS-15](CS-15-api-host-preview-endpoint-and-health-probes.md) | API host, preview endpoint and health probes | 4 | M | CS-13, CS-14 |
| [CS-16](CS-16-reservation-endpoints.md) | Reservation endpoints | 4 | S | CS-15 |
| [CS-17](CS-17-admin-policy-api-and-manifest-endpoint.md) | Admin policy API and manifest endpoint | 4 | M | CS-15 |
| [CS-18](CS-18-authentication-roles-and-the-test-token-startup.md) | Authentication, roles, and the test-token startup guard | 4 | M | CS-16, CS-17 |
| [CS-19](CS-19-structured-logging-correlation-and-domain-events.md) | Structured logging, correlation and domain events | 4 | M | CS-15 |
| [CS-20](CS-20-order-api-stand-in-with-authoritative-re-pricing.md) | Order API stand-in with authoritative re-pricing | 4 | M | CS-18 |
| [CS-21](CS-21-api-contract-test-suite.md) | API contract test suite | 4 | S | CS-19, CS-20 |
| [CS-22](CS-22-reqnroll-bdd-suite-with-a-configurable-target.md) | Reqnroll BDD suite with a configurable target | 5 | L | CS-21 |
| [CS-23](CS-23-cosmos-db-adapter-with-transactional-reservation.md) | Cosmos DB adapter with transactional reservation | 6 | L | CS-13, CS-14 |
| [CS-24](CS-24-cosmos-emulator-integration-tests.md) | Cosmos emulator integration tests | 6 | M | CS-23 |
| [CS-25](CS-25-bicep-modules-for-the-whole-environment.md) | Bicep modules for the whole environment | 7 | L | CS-01 |
| [CS-26](CS-26-azure-pipelines-definition-seeding-and-template.md) | Azure Pipelines definition, seeding and template linting | 7 | L | CS-22, CS-25 |

## Execution order

Tickets in the same batch have no dependency on each other, so their prompts can be running
at the same time in separate chats. A batch cannot start until the batch above it is merged.

| Batch | Tickets | Concurrency |
|---|---|---|
| 1 | CS-01 | one chat |
| 2 | CS-02, CS-03, CS-25 | 3 chats in parallel |
| 3 | CS-04, CS-05 | 2 chats in parallel |
| 4 | CS-06, CS-07 | 2 chats in parallel |
| 5 | CS-08, CS-09 | 2 chats in parallel |
| 6 | CS-10, CS-12 | 2 chats in parallel |
| 7 | CS-11, CS-13, CS-14 | 3 chats in parallel |
| 8 | CS-15, CS-23 | 2 chats in parallel |
| 9 | CS-16, CS-17, CS-19, CS-24 | 4 chats in parallel |
| 10 | CS-18 | one chat |
| 11 | CS-20 | one chat |
| 12 | CS-21 | one chat |
| 13 | CS-22 | one chat |
| 14 | CS-26 | one chat |

Running a batch in parallel means separate chats, not one chat given several prompts. Two
agents editing the same working tree will collide; give each its own worktree or branch.

## Blocked on Azure

CS-25, CS-26 are authored and linted but cannot be deployed until a subscription
exists. Their specifications say so explicitly, and instruct the agent never to report a
deployment that did not happen.

## Regenerating

```powershell
./scripts/generate-ticket-docs.ps1          # rewrite these files from tickets.json
./scripts/generate-ticket-docs.ps1 -Check   # fail if they are stale, for CI
```

---

<sub>Generated from `.github/tickets.json` by `scripts/generate-ticket-docs.ps1`.</sub>
