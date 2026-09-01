# CS-19: Structured logging, correlation and domain events

| | |
|---|---|
| **Wave** | 4 — API, auth, logging |
| **Size** | M |
| **Labels** | `wave-4, area:api, size:M` |
| **Blocked by** | [CS-15](CS-15-api-host-preview-endpoint-and-health-probes.md) |
| **Blocks** | [CS-21](CS-21-api-contract-test-suite.md) |

> **Read [`AGENTS.md`](../../AGENTS.md) before starting.** It carries the standing rules —
> money as `decimal`, engine purity, determinism via `IClock`, scope discipline, the definition
> of done and the git conventions — which are deliberately not repeated in every ticket.

## Goal

Make one customer complaint resolve to one trace, and keep sensitive values out of logs.

## Blocked by

- CS-15 — API host, preview endpoint and health probes

## Scope — touch only these paths

- `src/CouponService.Api/Observability/`
- `src/CouponService.Infrastructure/Logging/`
- `tests/CouponService.ApiTests/Observability/`

You may additionally add your own new test files, and tick the matching checkboxes in
`.kiro/specs/coupon-service/tasks.md`. Any other file you touch must be called out in the
pull request under a heading `Out-of-scope changes`.

## Out of scope

- Application Insights wiring and alert rules, which need Azure.

## Acceptance criteria

Defined in [`requirements.md`](../../.kiro/specs/coupon-service/requirements.md). Look each
one up and read the full text; do not infer it from the identifier. This ticket satisfies:

- **AC-8.1**
- **AC-8.2**
- **AC-8.3**
- **AC-8.4**

Each one needs a test that would fail without this change.

## Implementation notes

- Serilog with compact JSON output, enriched with CorrelationId, UserId, CouponCode, OrderId, PolicyContentHash, Outcome and DurationMs.
- Correlation middleware accepting an inbound W3C traceparent or generating one, echoing it on the response, and propagating it on outgoing calls.
- Emit the named domain events from the architecture document: CouponPreviewed, CouponApplied, CouponRejected, ReservationCreated, RedemptionConfirmed, ReservationReleased, ReservationExpired, UsageLimitReached, CouponServiceUnavailable.
- Add a redaction policy and a test that asserts no log line ever contains a bearer token, a connection string or a customer email, driven by a request that carries all three.
- Test that a correlation id supplied by the caller survives to the log output unchanged.

## Packages this ticket may add

- `Serilog.AspNetCore`
- `Serilog.Formatting.Compact`

Versions belong in `Directory.Packages.props`, not in the csproj. Adding a package that is
not listed here needs a justification in the pull request.

## Verification

All of these must pass, with zero warnings. Do not suppress an analyzer to get there.

```powershell
dotnet build CouponService.slnx
dotnet test tests/CouponService.ApiTests --filter FullyQualifiedName~Observability
dotnet test CouponService.slnx
```

## Prompt

Confirm CS-15 is merged, then paste this into a fresh Cursor
chat in this repository. Nothing else is needed: everything is either in the prompt or in a
file the prompt names.

```text
Implement ticket CS-19 in this repository, end to end.

Read these first, in order. They are the contract and they override anything you assume:
  1. .github/tickets/CS-19-structured-logging-correlation-and-domain-events.md
     This ticket: goal, scope, out of scope, acceptance criteria, implementation notes, verification.
  2. AGENTS.md
     Standing rules: money as decimal, engine purity, determinism via the injected IClock,
     scope discipline, warnings as errors, git conventions, PowerShell 5.1 environment.
  3. .kiro/specs/coupon-service/requirements.md
     The full text of AC-8.1, AC-8.2, AC-8.3, AC-8.4. Look each one up and read it. Do not
     infer an acceptance criterion from its identifier.
  4. .kiro/specs/coupon-service/design.md
     Decisions P-1 to P-12 and the exact type signatures. Overrides docs/solution-architecture.md
     where the two differ. Consult the architecture document for the reasoning behind a decision.

Then:
  1. Create branch ticket/CS-19-structured-logging-correlation from the latest main.
  2. Implement the ticket, touching only these paths:
       src/CouponService.Api/Observability/
       src/CouponService.Infrastructure/Logging/
       tests/CouponService.ApiTests/Observability/
     Plus your own new test files, and the checkboxes in .kiro/specs/coupon-service/tasks.md.
     Do not reformat or tidy a file you did not otherwise need to change. If you genuinely
     need a file outside this list, change it and say so in the pull request.
  3. You may add these packages, versions centralised in Directory.Packages.props: Serilog.AspNetCore, Serilog.Formatting.Compact
     Any other package needs a justification in the pull request.
  4. Verify. Every command must pass, with zero warnings:
       dotnet build CouponService.slnx
       dotnet test tests/CouponService.ApiTests --filter FullyQualifiedName~Observability
       dotnet test CouponService.slnx
     Each acceptance criterion needs a test that fails without your change. Prove that by
     reverting the change mentally, or temporarily, and confirming the test goes red.
  5. Tick the matching checkboxes in .kiro/specs/coupon-service/tasks.md.
  6. Commit in atomic, granular steps as you go - one logical change each, every commit
     building - with subjects of the form "CS-19: <imperative summary>". Do not squash the
     branch into a single commit; the granularity is what makes the pull request reviewable.
     Add no trailer of any kind: no Co-Authored-By, no Signed-off-by, no tool attribution.
  7. Push the branch and open a pull request against main titled "CS-19: Structured logging, correlation and domain events".
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
