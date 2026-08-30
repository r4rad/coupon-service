# Requirements: Coupon Service

## Overview

Add coupon support to a pizza ordering platform. A standalone Coupon Service validates coupon codes against a **data-driven policy engine**, calculates the resulting price, and enforces redemption limits across concurrent checkouts. Because no existing ordering platform was provided, a thin Order API stands in as the authoritative checkout caller.

Design of record: [docs/solution-architecture.md](../../../docs/solution-architecture.md). Planning deltas: [design.md](design.md).

**Delivery constraint.** The committed deadline was 2–3 days (hard). Requirements are split into `[D3]` — satisfied and verifiable without a live Azure subscription — and `[POST]` — need a green run against real Azure. A demo subscription and resource groups exist (`rg-coupon-demo` non-prod; `rg-coupon-prod` for production CD); `[POST]` work is ticketed as **CS-27 through CS-30**. CI and CD are **Azure Pipelines only** (CS-26, CS-29) with branching **feature → develop → main** (P-14) — there is no GitHub Actions workflow in this delivery.

---

## User Stories

### US-1: Price a basket with a coupon `[D3]`

**As a** customer, **I want** to enter a coupon code and immediately see what it does to my total, **so that** I can decide whether to order.

**Acceptance Criteria:**
- **AC-1.1** WHEN a basket and a valid, applicable coupon code are submitted to preview THE SYSTEM SHALL return the subtotal, the discount and the total, with the discount allocated per line item.
- **AC-1.2** WHEN a coupon does not apply THE SYSTEM SHALL return HTTP 200 with status `Rejected`, a machine-readable reason from the closed reason enum, and a full-price breakdown.
- **AC-1.3** WHEN a coupon is rejected because a numeric threshold was not met THE SYSTEM SHALL include a near-miss hint containing the shortfall amount.
- **AC-1.4** WHEN a fixed-amount discount exceeds the eligible base THE SYSTEM SHALL cap the discount at the base so the total is zero and never negative.
- **AC-1.5** WHEN any monetary value is produced THE SYSTEM SHALL compute it as `decimal`, round to two decimal places away from zero, and round exactly once at the point of production.
- **AC-1.6** WHEN preview is called THE SYSTEM SHALL NOT write any data, reserve a use, or consume a usage allowance.

### US-2: Express coupon rules as data `[D3]`

**As a** campaign manager, **I want** to define new coupon rules without a code change or deployment, **so that** campaigns are not blocked by release cycles.

**Acceptance Criteria:**
- **AC-2.1** WHEN a policy document is submitted THE SYSTEM SHALL parse a condition expressed with constant, fact, logical, comparison, membership, quantifier, aggregate and arithmetic node types.
- **AC-2.2** WHEN a policy document is submitted THE SYSTEM SHALL parse an effect expressed with `percentage`, `fixedAmount`, `cheapestFree`, `nthItem`, `tiered`, `bestOf`, `sum` and `cap`.
- **AC-2.3** WHEN a condition references a fact that is not in the fact registry THE SYSTEM SHALL reject the write with HTTP 400 identifying the offending node and fact path.
- **AC-2.4** WHEN a condition compares values of incompatible types THE SYSTEM SHALL reject the write with HTTP 400 rather than failing at evaluation time.
- **AC-2.5** WHEN a policy document exceeds the configured node-count or nesting-depth budget THE SYSTEM SHALL abort parsing and reject the write.
- **AC-2.6** WHEN a new rule is composed only from already-registered facts and effects THE SYSTEM SHALL apply it with no rebuild and no redeployment.
- **AC-2.7** WHEN a policy is evaluated THE SYSTEM SHALL produce identical output for identical inputs, taking all time values from an injected clock.
- **AC-2.8** WHEN a `bestOf` effect is evaluated THE SYSTEM SHALL compute every branch and select the branch yielding the largest discount.
- **AC-2.9** WHEN a `cap` binds a nested effect above its ceiling THE SYSTEM SHALL reduce the total to the ceiling and rescale per-line allocations proportionally so allocations sum to the capped total.
- **AC-2.10** WHEN the engine receives a policy whose `engineSchema` it does not implement THE SYSTEM SHALL refuse to evaluate it rather than guessing.

### US-3: Evaluate cheaply and explain the result `[D3]`

**As an** engineer, **I want** evaluation to avoid unnecessary work and to be inspectable, **so that** the service is cheap to run and failures are diagnosable.

**Acceptance Criteria:**
- **AC-3.1** WHEN a policy is compiled THE SYSTEM SHALL order the operands of `all` and `any` by ascending fact cost so pure predicates are evaluated before remote reads.
- **AC-3.2** WHEN a cheap operand of `all` evaluates false THE SYSTEM SHALL short-circuit and perform zero remote fact reads.
- **AC-3.3** WHEN the same fact is referenced more than once during one evaluation THE SYSTEM SHALL resolve it once and memoise the value for that evaluation.
- **AC-3.4** WHEN a policy document is unchanged THE SYSTEM SHALL reuse the compiled form, keyed on the SHA-256 hash of its canonical JSON.
- **AC-3.5** WHEN a policy document changes THE SYSTEM SHALL recompile on next use without an explicit cache invalidation call.
- **AC-3.6** WHEN a decision is produced THE SYSTEM SHALL record the policy content hash on it so a historical price can be reproduced after the policy is edited.
- **AC-3.7** WHEN the caller requests an explanation THE SYSTEM SHALL return a node-by-node evaluation trace.

### US-4: Enforce redemption limits under concurrency `[D3 logic] [POST verified on Cosmos]`

**As the** business, **I want** usage caps to hold even when orders are placed simultaneously, **so that** a limited campaign cannot be oversold.

**Acceptance Criteria:**
- **AC-4.1** WHEN checkout begins THE SYSTEM SHALL reserve the coupon before the order is committed and return an authoritative price breakdown.
- **AC-4.2** WHEN the order is committed THE SYSTEM SHALL confirm the reservation.
- **AC-4.3** WHEN the order fails after reservation THE SYSTEM SHALL release the reservation and return the use.
- **AC-4.4** WHEN a reservation is neither confirmed nor released THE SYSTEM SHALL expire it automatically after the configured TTL so an abandoned checkout cannot permanently consume a use.
- **AC-4.5** WHEN two reservations for the last remaining use are attempted concurrently THE SYSTEM SHALL grant exactly one and reject the other with HTTP 409 and reason `UsageLimitReached`.
- **AC-4.6** WHEN a reservation is retried with an `orderId` that already has one THE SYSTEM SHALL return the existing reservation and SHALL NOT increment any usage count.
- **AC-4.7** WHEN confirm or release is called twice for the same `orderId` THE SYSTEM SHALL treat the second call as a no-op.
- **AC-4.8** WHEN a per-customer limit is reached THE SYSTEM SHALL reject with reason `PerCustomerLimitReached`.

### US-5: Prevent client-side price tampering `[D3]`

**As the** business, **I want** the server to own the price, **so that** a manipulated browser cannot buy pizza at an invented total.

**Acceptance Criteria:**
- **AC-5.1** WHEN an order is submitted THE SYSTEM SHALL discard any total supplied by the client and re-price the basket server-side.
- **AC-5.2** WHEN an order is persisted THE SYSTEM SHALL store only the total returned by the Coupon Service.
- **AC-5.3** WHEN a coupon previewed successfully but fails at checkout THE SYSTEM SHALL place the order at full price with the rejection reason attached, unless the caller requested `RequireDiscount`, in which case it SHALL return HTTP 409.
- **AC-5.4** WHEN the Coupon Service is unreachable at checkout THE SYSTEM SHALL place the order at full price, log the degradation and raise an alert, rather than failing the order or honouring an unverified discount.

### US-6: Manage policies through an API `[D3 for CRUD + manifest] [POST for simulate]`

**As a** campaign manager, **I want** to create and govern policies through an API, **so that** campaigns are safe to change without an engineer.

**Acceptance Criteria:**
- **AC-6.1** WHEN an administrator creates or updates a policy THE SYSTEM SHALL validate it against the engine manifest before persisting it.
- **AC-6.2** WHEN an administrator requests the manifest THE SYSTEM SHALL return every registered fact with its type and cost, every operator, every effect and every configured limit.
- **AC-6.3** WHEN an administrator updates a policy THE SYSTEM SHALL require a matching ETag and SHALL reject a stale write with HTTP 412.
- **AC-6.4** WHEN an administrator deletes a policy THE SYSTEM SHALL move it to `Archived` and SHALL NOT remove the document, so historical orders stay explainable.
- **AC-6.5** WHEN an administrator simulates a candidate policy against a sample basket THE SYSTEM SHALL return the decision and full trace and SHALL persist nothing. `[POST]`
- **AC-6.6** WHEN a policy is in `Shadow` status and would apply THE SYSTEM SHALL record what it would have granted and SHALL apply a discount of zero. `[POST]`
- **AC-6.7** WHEN a policy has no coupon code THE SYSTEM SHALL evaluate it automatically for baskets previewed without a code.

### US-7: Authenticate and authorise every call `[D3 locally] [POST against Entra]`

**As a** security reviewer, **I want** each caller to prove who it is and to reach only what it needs, **so that** privileged operations cannot be invoked from a browser.

**Acceptance Criteria:**
- **AC-7.1** WHEN a request arrives without a valid bearer token THE SYSTEM SHALL return HTTP 401.
- **AC-7.2** WHEN a token is valid but lacks the required application role THE SYSTEM SHALL return HTTP 403.
- **AC-7.3** WHEN reserve, confirm or release is called THE SYSTEM SHALL require the `Coupon.Redeem` role.
- **AC-7.4** WHEN any admin endpoint is called THE SYSTEM SHALL require the `Coupon.Admin` role.
- **AC-7.5** WHEN the service runs outside `Development` or `Test` THE SYSTEM SHALL NOT register the test token scheme, and SHALL fail startup if configuration attempts to enable it.
- **AC-7.6** WHEN deployed THE SYSTEM SHALL validate the token at the gateway and again in the application. `[POST]`
- **AC-7.7** WHEN the Order API calls the Coupon Service THE SYSTEM SHALL authenticate with a managed identity and no shared secret. `[POST]`

### US-8: Support troubleshooting through logs `[D3]`

**As an** operator, **I want** one request to be traceable across services, **so that** a customer complaint resolves to evidence.

**Acceptance Criteria:**
- **AC-8.1** WHEN any request is handled THE SYSTEM SHALL emit structured JSON logs carrying correlation id, outcome and duration.
- **AC-8.2** WHEN a request crosses a service boundary THE SYSTEM SHALL propagate the same correlation id.
- **AC-8.3** WHEN a coupon is previewed, applied, rejected, reserved, confirmed, released or expired THE SYSTEM SHALL emit a named domain event with the coupon code, order id and policy content hash where applicable.
- **AC-8.4** WHEN logging any event THE SYSTEM SHALL NOT write bearer tokens, connection strings, keys or customer contact details.
- **AC-8.5** WHEN a Cosmos operation completes THE SYSTEM SHALL log its request charge as a structured field. `[POST]`

### US-9: Deploy from nothing, with no manual steps `[POST]`

**As the** reviewer, **I want** the whole solution provisioned and deployed by a pipeline, **so that** the deployment is reproducible.

**Acceptance Criteria:**
- **AC-9.1** WHEN the pipeline runs against an empty resource group THE SYSTEM SHALL provision every resource it needs with no manual portal configuration.
- **AC-9.2** WHEN infrastructure is deployed THE SYSTEM SHALL publish a `what-if` result before applying changes.
- **AC-9.3** WHEN the pipeline authenticates to Azure THE SYSTEM SHALL use a workload-identity federated credential and no stored secret.
- **AC-9.4** WHEN unit tests fail THE SYSTEM SHALL fail the pipeline before anything is deployed.
- **AC-9.5** WHEN deployment completes THE SYSTEM SHALL seed a deterministic policy set idempotently.
- **AC-9.6** WHEN the pipeline is re-run THE SYSTEM SHALL converge to the same result without manual cleanup.
- **AC-9.7** WHEN the API is exposed THE SYSTEM SHALL route it through Azure API Management with JWT validation and rate limiting.

### US-10: Prove behaviour with automated tests `[D3 locally] [POST through APIM]`

**As the** reviewer, **I want** behaviour described in business language and verified automatically, **so that** the service's claims are checkable.

**Acceptance Criteria:**
- **AC-10.1** WHEN the test suite runs THE SYSTEM SHALL include BDD scenarios written in Gherkin covering coupon validation and order pricing.
- **AC-10.2** WHEN engine tests run THE SYSTEM SHALL verify that a failing cheap predicate causes zero remote fact resolutions.
- **AC-10.3** WHEN property-based tests run THE SYSTEM SHALL assert that no generated basket and policy combination produces a negative discount or one exceeding the eligible base.
- **AC-10.4** WHEN the BDD suite runs THE SYSTEM SHALL create its own run-scoped policy data and remove it afterwards, so repeated runs do not contradict each other.
- **AC-10.5** WHEN unit and engine tests run THE SYSTEM SHALL require no database, emulator or network.
- **AC-10.6** WHEN integration tests run THE SYSTEM SHALL exercise the real Cosmos data access path against the emulator. `[POST]`

---

## Non-Functional Requirements

- **NFR-1** WHEN evaluating a policy THE SYSTEM SHALL perform at most one point read to resolve the policy document.
- **NFR-2** WHEN deployed to demo SKUs THE SYSTEM SHALL answer 99% of previews within 800 ms and 99% of reservations within 1 s, measured at the gateway, excluding cold starts. `[POST]`
- **NFR-3** WHEN handling money THE SYSTEM SHALL never use binary floating point.
- **NFR-4** WHEN caching compiled policies THE SYSTEM SHALL bound cache size and expire entries, so neither memory nor code enumeration is unbounded.
- **NFR-5** WHEN the engine executes a policy THE SYSTEM SHALL provide no mechanism to execute arbitrary code, scripts or regular expressions supplied in policy data.
- **NFR-6** WHEN running in the demo environment THE SYSTEM SHALL stay within free or near-free Azure SKUs, with Container Registry Basic the only accepted charge. `[POST]`
- **NFR-7** WHEN source is committed THE SYSTEM SHALL build with analyzers enabled and warnings treated as errors.

---

## Out of Scope

- Payment capture, refunds, invoicing, tax engines
- Loyalty points, gift cards, referrals, stacking more than one coupon per order
- Administrative web UI — the admin API is in scope, a UI is not
- Multi-region, VNet, Private Link, WAF, Front Door, paid APIM tiers
- Load testing and penetration testing
- Kitchen, delivery, inventory and order tracking
- Migrating or wrapping any pre-existing ordering system
- React frontend, unless phases 0–6 complete comfortably

---

## Open Questions

- [ ] Currency: assumed `EUR`, single currency per deployment. Confirm.
- [ ] Does a real ordering platform exist that should replace the stand-in Order API?
- [ ] Azure region for provisioning.
- [ ] Whether the client's Azure DevOps organisation already has a parallel-job grant.
