# CS-18: Authentication, roles, and the test-token startup guard

| | |
|---|---|
| **Wave** | 4 — API, auth, logging |
| **Size** | M |
| **Labels** | `wave-4, area:security, size:M` |
| **Blocked by** | [CS-16](CS-16-reservation-endpoints.md), [CS-17](CS-17-admin-policy-api-and-manifest-endpoint.md) |
| **Blocks** | [CS-20](CS-20-order-api-stand-in-with-authoritative-re-pricing.md) |

> **Read [`AGENTS.md`](../../AGENTS.md) before starting.** It carries the standing rules —
> money as `decimal`, engine purity, determinism via `IClock`, scope discipline, the definition
> of done and the git conventions — which are deliberately not repeated in every ticket.

## Goal

Enforce identity and least privilege, and make the local test-token mechanism provably impossible to enable in a deployed environment. Decision P-8 exists because this is graded.

## Blocked by

- CS-16 — Reservation endpoints
- CS-17 — Admin policy API and manifest endpoint

## Scope — touch only these paths

- `src/CouponService.Api/Authentication/`
- `src/CouponService.Api/Program.cs`
- `tests/CouponService.ApiTests/Auth/`

You may additionally add your own new test files, and tick the matching checkboxes in
`.kiro/specs/coupon-service/tasks.md`. Any other file you touch must be called out in the
pull request under a heading `Out-of-scope changes`.

## Out of scope

- Real Entra tenant configuration, which needs credentials we do not have. Bind it from options with placeholders.

## Acceptance criteria

Defined in [`requirements.md`](../../.kiro/specs/coupon-service/requirements.md). Look each
one up and read the full text; do not infer it from the identifier. This ticket satisfies:

- **AC-7.1**
- **AC-7.2**
- **AC-7.3**
- **AC-7.4**
- **AC-7.5**

Each one needs a test that would fail without this change.

## Implementation notes

- Configure JWT bearer from options for authority, audience and issuer, ready to point at Entra without a code change.
- Authorisation policies requiring the Coupon.Redeem role on reservation endpoints and Coupon.Admin on admin endpoints.
- Add a test-token scheme signed with a symmetric development key, registered only when the environment is Development or Test.
- Add a startup guard that throws if the test scheme is enabled while the environment is anything else. Configuration alone must not be able to turn it on in production.
- The guard needs its own test: build a host with the test scheme configured and the environment set to Production, and assert startup throws.
- Test 401 with no token, 403 for a customer token on a reservation endpoint, and 200 for a token carrying the right role.

## Packages this ticket may add

- `Microsoft.AspNetCore.Authentication.JwtBearer`

Versions belong in `Directory.Packages.props`, not in the csproj. Adding a package that is
not listed here needs a justification in the pull request.

## Verification

All of these must pass, with zero warnings. Do not suppress an analyzer to get there.

```powershell
dotnet build CouponService.slnx
dotnet test tests/CouponService.ApiTests --filter FullyQualifiedName~Auth
dotnet test CouponService.slnx
```

## Prompt

Confirm CS-16 and CS-17 are merged, then paste this into a fresh Cursor
chat in this repository. Nothing else is needed: everything is either in the prompt or in a
file the prompt names.

```text
Implement ticket CS-18 in this repository, end to end.

Read these first, in order. They are the contract and they override anything you assume:
  1. .github/tickets/CS-18-authentication-roles-and-the-test-token-startup.md
     This ticket: goal, scope, out of scope, acceptance criteria, implementation notes, verification.
  2. AGENTS.md
     Standing rules: money as decimal, engine purity, determinism via the injected IClock,
     scope discipline, warnings as errors, git conventions, PowerShell 5.1 environment.
  3. .kiro/specs/coupon-service/requirements.md
     The full text of AC-7.1, AC-7.2, AC-7.3, AC-7.4, AC-7.5. Look each one up and read it. Do not
     infer an acceptance criterion from its identifier.
  4. .kiro/specs/coupon-service/design.md
     Decisions P-1 to P-12 and the exact type signatures. Overrides docs/solution-architecture.md
     where the two differ. Consult the architecture document for the reasoning behind a decision.

Then:
  1. Create branch ticket/CS-18-authentication-roles-and-the-test-token from the latest main.
  2. Implement the ticket, touching only these paths:
       src/CouponService.Api/Authentication/
       src/CouponService.Api/Program.cs
       tests/CouponService.ApiTests/Auth/
     Plus your own new test files, and the checkboxes in .kiro/specs/coupon-service/tasks.md.
     Do not reformat or tidy a file you did not otherwise need to change. If you genuinely
     need a file outside this list, change it and say so in the pull request.
  3. You may add these packages, versions centralised in Directory.Packages.props: Microsoft.AspNetCore.Authentication.JwtBearer
     Any other package needs a justification in the pull request.
  4. Verify. Every command must pass, with zero warnings:
       dotnet build CouponService.slnx
       dotnet test tests/CouponService.ApiTests --filter FullyQualifiedName~Auth
       dotnet test CouponService.slnx
     Each acceptance criterion needs a test that fails without your change. Prove that by
     reverting the change mentally, or temporarily, and confirming the test goes red.
  5. Tick the matching checkboxes in .kiro/specs/coupon-service/tasks.md.
  6. Commit as "CS-18: <imperative summary>", push the branch, and open a pull request titled
     "CS-18: Authentication, roles, and the test-token startup guard".
     In the body, list every acceptance criterion satisfied, anything deliberately deferred,
     and any out-of-scope change you had to make.

Rules of engagement:
  - Do not suppress an analyzer, add NoWarn, or weaken a test to get a green build. Fix the code.
  - Do not stub an acceptance criterion and mark it done.
  - Never report a result you did not observe.
  - If you become blocked, stop and report what you tried, what blocked you, and the options you
    see. A ticket returned with a clear blocker is a good outcome; one returned green with a
    hollowed-out test is not.
  - Read the neighbouring code before adding to it, and match its idiom.

When you finish, summarise: what you changed, which acceptance criteria are now covered by which
tests, the verification output, and anything you deferred or could not do.
```

## Definition of done

1. `dotnet build CouponService.slnx` succeeds with zero warnings.
2. Every command in Verification passes.
3. No previously passing test now fails.
4. Every acceptance criterion above is covered by a test that would fail without this change.
5. The matching checkboxes in `tasks.md` are ticked.
6. A pull request exists listing the acceptance criteria satisfied and anything deferred.

---

<sub>Generated from `.github/tickets.json` by `scripts/generate-ticket-docs.ps1`. Edit the JSON, then regenerate.</sub>
