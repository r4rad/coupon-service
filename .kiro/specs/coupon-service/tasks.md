# Implementation Tasks: Coupon Service

> Status: Not Started
> Last synced: 2026-08-27
> Requirements: [requirements.md](requirements.md) · Planning deltas: [design.md](design.md) · Design of record: [docs/solution-architecture.md](../../../docs/solution-architecture.md)

Hour figures are focused-work estimates. `[D3]` means inside the committed 2–3 day window; `[POST]` means after Azure access arrives. See **Three-day budget** at the end — the waves total more than the window, and the cut line is a decision, not an accident.

---

## Wave 0 — Foundations (no dependencies) · 1 h `[D3]`

- [ ] 0.1 `git init`, `.gitignore`, `.editorconfig`, initial commit of the existing docs (0.25 h)
- [x] 0.2 `Directory.Build.props`: .NET 10, nullable enabled, analyzers on, warnings as errors _(NFR-7)_ (0.25 h)
- [x] 0.3 Solution plus all project skeletons and reference edges per design.md (0.25 h)
- [x] 0.4 Architecture test asserting `CouponService.Engine` references nothing outside `Domain` _(AC-10.5)_ (0.25 h)
- [ ] 0.5 Create the Azure DevOps project as **public** and push, so parallel jobs are available without the grant wait (0.25 h, blocking — do first)

## Wave 1 — Engine core (depends: Wave 0) · 9 h `[D3]`

- [x] 1.1 Domain primitives: `Cart`, `CartLine`, `Money` rounding helpers, `PriceBreakdown`, `DiscountPlan`, `LineAllocation` _(AC-1.5, NFR-3)_ (0.75 h)
- [x] 1.2 `Value` and `ValueKind` _(design.md signatures)_ (0.5 h)
- [x] 1.3 `Expr` record hierarchy, operator enums, `Selector` (0.75 h)
- [x] 1.4 Parser with `ParseBudget` and `PolicySyntaxException`; single-key operator dispatch _(AC-2.1, AC-2.5)_ (1.5 h)
- [x] 1.5 Fact registry, `FactDescriptor`, `FactCost`, and the documented fact vocabulary (1 h)
- [x] 1.6 Manifest generated from the registry _(AC-6.2)_ (0.5 h)
- [x] 1.7 Validator: unknown facts, type compatibility, quantifier scope rules; reports **all** offending nodes _(AC-2.3, AC-2.4)_ (1.25 h)
- [x] 1.8 Compiler: closures over delegates, cost-ordered `all`/`any`, node ids _(AC-3.1, P-6)_ (1.5 h)
- [x] 1.9 `EvalScope`: fact memoisation, injected clock, trace collector, near-miss capture _(AC-1.3, AC-3.3, AC-3.7, AC-2.7)_ (1 h)
- [x] 1.10 Canonical JSON hashing and bounded compiled-policy cache _(AC-3.4, AC-3.5, NFR-4)_ (0.75 h)
- [ ] 1.11 Engine tests: grammar accept/reject, truth tables, budget enforcement, cost ordering, and the **counting fact provider proving zero remote reads** _(AC-10.2)_ (1.5 h) — folded into the estimates above

## Wave 2 — Effects and pricing (depends: 1.8, 1.9) · 5 h `[D3]`

- [x] 2.1 Selector evaluation and `EffectScope` (0.5 h)
- [x] 2.2 `percentage` and `fixedAmount` handlers _(AC-1.4)_ (0.75 h)
- [x] 2.3 `cap` with proportional allocation rescale and re-rounding _(AC-2.9)_ (0.75 h)
- [x] 2.4 `bestOf` and `sum` handlers _(AC-2.8)_ (0.75 h)
- [x] 2.5 `cheapestFree`, `nthItem`, `tiered` handlers _(AC-2.2, P-2)_ (1.25 h)
- [x] 2.6 `PriceCalculator` producing a breakdown from a decision _(AC-1.1)_ (0.5 h)
- [x] 2.7 Property-based tests: discount never negative, never above eligible base, allocations always sum to total _(AC-10.3)_ (0.5 h)

## Wave 3 — Application services and redemption (depends: Wave 2) · 5 h `[D3]`

- [ ] 3.1 `ICouponValidator`, `IPriceCalculator`, `ICouponRedeemer`, `PolicyDecision`, repository ports (0.75 h)
- [ ] 3.2 In-memory repositories with ETag semantics faithful enough to test CAS _(P-9)_ (1 h)
- [ ] 3.3 Redemption lifecycle: reserve, confirm, release, TTL stamping, idempotency on `orderId` _(AC-4.1…AC-4.7)_ (1.75 h)
- [ ] 3.4 `IAutomaticPolicyIndex` with 60-second cache and priority-based resolution _(AC-6.7, P-4)_ (0.75 h)
- [ ] 3.5 Unit tests including a simulated concurrent reservation race _(AC-4.5)_ (0.75 h)

## Wave 4 — API, auth and logging (depends: Wave 3) · 7 h `[D3]`

- [ ] 4.1 API host, DI wiring, options binding, OpenAPI (0.75 h)
- [ ] 4.2 Preview endpoint, problem+json, closed rejection-reason enum _(AC-1.2)_ (1 h)
- [ ] 4.3 Reservations endpoints _(AC-4.1…AC-4.3)_ (0.75 h)
- [ ] 4.4 Admin endpoints: create and list with manifest validation, read, update with ETag, archive on delete _(AC-6.1, AC-6.3, AC-6.4)_ (1.5 h)
- [ ] 4.5 Auth: JWT bearer, role policies, test token scheme, **startup guard** plus the test that proves it fires _(AC-7.1…AC-7.5, P-8)_ (1.25 h)
- [ ] 4.6 Serilog JSON, correlation middleware, named domain events, redaction _(AC-8.1…AC-8.4)_ (1 h)
- [ ] 4.7 Health endpoints `/live` and `/ready` (0.25 h)
- [ ] 4.8 Order API: catalog from `data/pizzas.json`, `POST /orders`, reserve then confirm, client total discarded _(AC-5.1…AC-5.4)_ (1 h)
- [ ] 4.9 `WebApplicationFactory` contract tests: 200-with-rejection, 400 shapes, 401, 403 (0.5 h)

## Wave 5 — BDD suite (depends: Wave 4) · 3 h `[D3]`

- [ ] 5.1 Reqnroll project, configurable base URL and token provider so the same features run locally and via APIM later (0.75 h)
- [ ] 5.2 Feature files covering the eleven scenarios in the design of record _(AC-10.1)_ (1 h)
- [ ] 5.3 Step definitions, run-scoped policy prefix, seed and teardown hooks _(AC-10.4)_ (1.25 h)

## Wave 6 — Cosmos adapter (depends: Wave 3) · 4 h `[D3 code] [POST verified]`

- [ ] 6.1 Cosmos repositories, three containers, `/pk` convention, unique key on `/orderId`, TTL _(P-3, P-10)_ (1.5 h)
- [ ] 6.2 Transactional-batch reserve with ETag precondition and jittered retry _(AC-4.5)_ (1 h)
- [ ] 6.3 `docker compose` emulator plus integration tests, skipping cleanly when unavailable _(AC-10.6, P-9)_ (1.5 h)

## Wave 7 — Infrastructure and pipeline, authored (depends: Wave 4) · 6 h `[D3 code] [POST run]`

- [ ] 7.1 Bicep modules: observability, identity, key vault, cosmos, container apps, apim, apim-api, static web app (2.5 h)
- [ ] 7.2 `main.bicep`, demo parameters, placeholder-image pattern _(P-11)_ (1 h)
- [ ] 7.3 `bicep build` and lint wired into the build stage so the templates are verified without a subscription _(AC-9.2)_ (0.5 h)
- [ ] 7.4 `azure-pipelines.yml`, eight stages with gates _(AC-9.1, AC-9.3, AC-9.4)_ (1.5 h)
- [ ] 7.5 Idempotent seeding script over the admin API _(AC-9.5, AC-9.6)_ (0.5 h)

## Wave 8 — After Azure access `[POST]`

- [ ] 8.1 Provision into an empty resource group and iterate to a green deployment _(AC-9.1)_
- [ ] 8.2 APIM products, policies, JWT validation and rate limiting _(AC-9.7, AC-7.6)_
- [ ] 8.3 Managed identity and app roles for the internal hop _(AC-7.7)_
- [ ] 8.4 Switch the BDD suite to client credentials and run it through APIM
- [ ] 8.5 `simulate` endpoint and shadow-mode evaluation _(AC-6.5, AC-6.6)_
- [ ] 8.6 Alerts, workbook, request-charge logging _(AC-8.5, NFR-2)_
- [ ] 8.7 Correct the APIM cache claim in the design of record _(P-12)_
- [ ] 8.8 `docs/deployment.md`, `docs/authentication.md`, assumptions write-up
- [ ] 8.9 React SPA, only if everything above is comfortable

---

## Three-day budget

Waves 0–7 total **40 hours**. At 9 focused hours per day the window holds **27**. The shortfall is 13 hours, so three waves cannot all fit and the choice of casualty is yours.

| Sequence | Cumulative |
|---|---|
| Wave 0 | 1 h |
| Wave 1 — engine core | 10 h |
| Wave 2 — effects and pricing | 15 h |
| Wave 3 — application and redemption | 20 h |
| Wave 4 — API, auth, logging | 27 h ← **window ends here** |
| Wave 5 — BDD | 30 h |
| Wave 6 — Cosmos adapter | 34 h |
| Wave 7 — infrastructure authored | 40 h |

Waves 5, 6 and 7 each map to something the brief grades explicitly — a BDD project, the data layer, and a pipeline that deploys from scratch. Delivering none of them is worse than delivering all three thinly, so the 13 hours has to come out of waves 1–4.

### Candidate casualties, cheapest first

| Cut | Buys | Cost of cutting |
|---|---|---|
| Admin update, ETag and archive (4.4 partial) | 1.0 h | Loses AC-6.3 and AC-6.4; create, list and read remain |
| Automatic policies (3.4) | 0.75 h | Loses AC-6.7 and one BDD scenario; the `/pk` design still supports adding them later |
| `cheapestFree`, `nthItem`, `tiered` (2.5) | 1.25 h | Reverses decision P-2; weakens the "any future rule" claim |
| Property-based tests (2.7) | 0.5 h | Loses AC-10.3, a distinctive piece of rigour |
| Order API persistence to Cosmos (part of 4.8, 6.1) | 1.0 h | Reverses P-10; orders vanish on replica recycle |
| Cosmos adapter, keeping in-memory only (Wave 6) | 4.0 h | Data-layer code absent, which a reviewer will notice |
| Trace endpoint and explain mode (part of 1.9) | 0.5 h | Loses AC-3.7; near-miss hints survive |

Cutting the first five buys 4.5 hours. Reaching 13 requires taking Wave 6 as well, or extending the window by a day.
