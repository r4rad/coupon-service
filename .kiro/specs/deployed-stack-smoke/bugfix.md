# Bugfix: the CD stage that verifies a deployment cannot pass

## Symptom

CD stage 7 ("Reqnroll BDD against deployed stack") fails all 11 scenarios in
`BeforeTestRun`:

```
Coupon Service at 'https://apim-coupon-dev-....azure-api.net/coupons/'
returned 404 for /v1/health/live. The BDD target is unreachable or not ready.
```

## Current behaviour

Two independent faults, one a defect and one a design mismatch.

**The defect.** `BddHost` sets `BaseAddress` to `https://<apim>/coupons/` and
requests `/v1/health/live`. A root-relative request URI is resolved against the
authority alone, so `/coupons` is discarded and the request reaches the gateway
root, where no API is published. Measured against the live dev gateway:

| Request | Result |
| --- | --- |
| `GET /coupons/v1/health/live` | 200 |
| `GET /v1/health/live` | 404 |
| `GET /coupons/v1/health/ready` | 200 |

**The design mismatch.** Removing the 404 does not make the stage pass, because
the suite is built for an in-process host:

1. `BDD_Bdd__TokenStrategy: TestToken`, but APIM's customer product runs
   `validate-jwt` against Entra on every non-health route, and the deployed app
   sets `Authentication__TestToken__Enabled: 'false'`. Measured:
   `POST /coupons/v1/coupons/preview` anonymously returns 401.
2. AC-10.4 run-scoped seeding posts to `/v1/admin/policies`. APIM publishes the
   admin API under path `admin` with `subscriptionRequired: true`, so it needs a
   different base URL, a real Entra admin JWT and a subscription key. Measured:
   `POST /coupons/v1/admin/policies` returns 404.
3. The scenarios drive `MutableClock`, injected only into the in-process
   factory. In `Http` mode `Clock.Advance` moves a clock nothing observes, and
   `TuesdayAutomatic` asserts `time.localDayOfWeek == Tuesday`, which cannot
   hold deterministically against a real deployment.

Fault 3 has no fix that preserves the scenarios: a deployed service has no
clock the test can control.

## Expected behaviour

The full Reqnroll suite runs in CI, in process, where the clock and the policy
store are controllable. It already does — the `Test` stage runs
`dotnet test CouponService.slnx`, which includes `CouponService.Bdd` in its
default `InProcess` mode. That is what AC-10.1 and AC-10.4 ask for, and neither
criterion mentions a deployed target.

CD stage 7 becomes a **smoke** stage that verifies the deployment itself, using
only what a hardened deployment will honestly answer:

| Check | Proves |
| --- | --- |
| `GET /coupons/v1/health/live` is 200 | the app is deployed and serving |
| `GET /coupons/v1/health/ready` is 200 | the startup seed succeeded (AC-9.5, AC-9.6) |
| `POST /coupons/v1/coupons/preview` anonymously is 401 | the gateway enforces JWT (AC-7.6, AC-9.7) |
| the same request with a real Entra token is 200, discount 4.00 on a 40.00 basket | a seeded policy evaluates end to end through the gateway |
| `GET /orders/v1/pizzas` anonymously is 401 | the Order API is published and protected |

The token comes from the pipeline's existing workload-identity federation via
`az account get-access-token --resource <couponApiAudience>`. No app role is
required: the app authorizes only reservations (`Coupon.Redeem`) and admin
(`Coupon.Admin`), so preview needs authentication but no role, and APIM's
customer policy checks audience and issuer without a role claim.

The base-path defect is still fixed, because `Http` mode remains supported for
pointing the suite at a locally running stack.

## Unchanged behaviour

- The 11 BDD scenarios, their Gherkin, and their in-process execution in CI.
- `MutableClock`, `TokenProvider` and the test-token scheme (P-8).
- The startup seeder and the Seed stage's readiness verification.
- Stage 8 `Verify`, which smokes the backend directly and summarises.
- Every APIM policy, Entra app registration and app-role assignment.
