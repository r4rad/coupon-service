# CS-15: API host, preview endpoint and health probes

| | |
|---|---|
| **Wave** | 4 — API, auth, logging |
| **Size** | M |
| **Labels** | `wave-4, area:api, size:M` |
| **Blocked by** | [CS-13](CS-13-redemption-lifecycle-reserve-confirm-release.md), [CS-14](CS-14-automatic-policies-storage-key-cached-index.md) |
| **Blocks** | [CS-16](CS-16-reservation-endpoints.md), [CS-17](CS-17-admin-policy-api-and-manifest-endpoint.md), [CS-19](CS-19-structured-logging-correlation-and-domain-events.md) |

> **Read [`AGENTS.md`](../../AGENTS.md) before starting.** It carries the standing rules —
> money as `decimal`, engine purity, determinism via `IClock`, scope discipline, the definition
> of done and the git conventions — which are deliberately not repeated in every ticket.

## Goal

Expose preview over HTTP with the documented contract, where a rejected coupon is a business outcome and not a transport error.

## Blocked by

- CS-13 — Redemption lifecycle: reserve, confirm, release, expire
- CS-14 — Automatic policies: storage key, cached index and resolution

## Scope — touch only these paths

- `src/CouponService.Api/`
- `tests/CouponService.ApiTests/Preview/`

You may additionally add your own new test files, and tick the matching checkboxes in
`.kiro/specs/coupon-service/tasks.md`. Any other file you touch must be called out in the
pull request under a heading `Out-of-scope changes`.

## Out of scope

- Auth, which is CS-18.
- Serilog, which is CS-19.
- Admin endpoints, which are CS-17.

## Acceptance criteria

Defined in [`requirements.md`](../../.kiro/specs/coupon-service/requirements.md). Look each
one up and read the full text; do not infer it from the identifier. This ticket satisfies:

- **AC-1.2**
- **AC-1.3**
- **AC-1.6**

Each one needs a test that would fail without this change.

## Implementation notes

- Set up the host, DI, strongly typed options with validation on start, and OpenAPI.
- POST /v1/coupons/preview returns 200 for both applied and rejected, with status, reason, near-miss hint and a full breakdown.
- Return RFC 7807 problem details for genuine errors, always including the correlation id.
- Add /health/live and /health/ready, where readiness checks the policy repository.
- Version every route under /v1 so APIM routing mirrors it later.
- Test that an expired coupon returns 200 with reason Expired, that a malformed body returns 400 with field detail, and that preview writes nothing.

## Verification

All of these must pass, with zero warnings. Do not suppress an analyzer to get there.

```powershell
dotnet build CouponService.slnx
dotnet test tests/CouponService.ApiTests --filter FullyQualifiedName~Preview
dotnet test CouponService.slnx
```

## Prompt

Confirm CS-13 and CS-14 are merged, then paste this into a fresh Cursor
chat in this repository. Nothing else is needed: everything is either in the prompt or in a
file the prompt names.

```text
Implement ticket CS-15 in this repository, end to end.

Read these first, in order. They are the contract and they override anything you assume:
  1. .github/tickets/CS-15-api-host-preview-endpoint-and-health-probes.md
     This ticket: goal, scope, out of scope, acceptance criteria, implementation notes, verification.
  2. AGENTS.md
     Standing rules: money as decimal, engine purity, determinism via the injected IClock,
     scope discipline, warnings as errors, git conventions, PowerShell 5.1 environment.
  3. .kiro/specs/coupon-service/requirements.md
     The full text of AC-1.2, AC-1.3, AC-1.6. Look each one up and read it. Do not
     infer an acceptance criterion from its identifier.
  4. .kiro/specs/coupon-service/design.md
     Decisions P-1 to P-12 and the exact type signatures. Overrides docs/solution-architecture.md
     where the two differ. Consult the architecture document for the reasoning behind a decision.

Then:
  1. Create branch ticket/CS-15-api-host-preview-endpoint-and-health from the latest main.
  2. Implement the ticket, touching only these paths:
       src/CouponService.Api/
       tests/CouponService.ApiTests/Preview/
     Plus your own new test files, and the checkboxes in .kiro/specs/coupon-service/tasks.md.
     Do not reformat or tidy a file you did not otherwise need to change. If you genuinely
     need a file outside this list, change it and say so in the pull request.
  3. Add no NuGet package. If you believe one is required, stop and say why.
  4. Verify. Every command must pass, with zero warnings:
       dotnet build CouponService.slnx
       dotnet test tests/CouponService.ApiTests --filter FullyQualifiedName~Preview
       dotnet test CouponService.slnx
     Each acceptance criterion needs a test that fails without your change. Prove that by
     reverting the change mentally, or temporarily, and confirming the test goes red.
  5. Tick the matching checkboxes in .kiro/specs/coupon-service/tasks.md.
  6. Commit in atomic, granular steps as you go - one logical change each, every commit
     building - with subjects of the form "CS-15: <imperative summary>". Do not squash the
     branch into a single commit; the granularity is what makes the pull request reviewable.
     Add no trailer of any kind: no Co-Authored-By, no Signed-off-by, no tool attribution.
  7. Push the branch and open a pull request titled "CS-15: API host, preview endpoint and health probes".
     In the body, list every acceptance criterion satisfied, anything deliberately deferred,
     and any out-of-scope change you had to make.

Rules of engagement:
  - Do not suppress an analyzer, add NoWarn, or weaken a test to get a green build. Fix the code.
  - Do not stub an acceptance criterion and mark it done.
  - Never report a result you did not observe.
  - Comment only what the code cannot express: an invariant, an external constraint, a
    non-obvious trade-off, or the AC or P item that forces a behaviour. Do not narrate the
    code, and never explain your change in a comment - that belongs in the commit message.
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
6. The branch is a sequence of atomic commits, none of them carrying a trailer.
7. A pull request exists listing the acceptance criteria satisfied and anything deferred.

---

<sub>Generated from `.github/tickets.json` by `scripts/generate-ticket-docs.ps1`. Edit the JSON, then regenerate.</sub>
