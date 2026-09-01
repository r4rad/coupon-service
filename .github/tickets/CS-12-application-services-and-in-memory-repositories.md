# CS-12: Application services and in-memory repositories

| | |
|---|---|
| **Wave** | 3 — Application services and redemption |
| **Size** | M |
| **Labels** | `wave-3, area:domain, size:M` |
| **Blocked by** | [CS-08](CS-08-content-hashing-and-the-bounded-compiled-policy.md), [CS-09](CS-09-core-effect-handlers-and-the-price-calculator.md) |
| **Blocks** | [CS-13](CS-13-redemption-lifecycle-reserve-confirm-release.md), [CS-14](CS-14-automatic-policies-storage-key-cached-index.md) |

> **Read [`AGENTS.md`](../../AGENTS.md) before starting.** It carries the standing rules —
> money as `decimal`, engine purity, determinism via `IClock`, scope discipline, the definition
> of done and the git conventions — which are deliberately not repeated in every ticket.

## Goal

Put the clean interfaces from the brief in place, with in-memory persistence faithful enough to test optimistic concurrency without a database.

## Blocked by

- CS-08 — Content hashing and the bounded compiled-policy cache
- CS-09 — Core effect handlers and the price calculator

## Scope — touch only these paths

- `src/CouponService.Application/`
- `src/CouponService.Infrastructure/InMemory/`
- `tests/CouponService.UnitTests/Application/`

You may additionally add your own new test files, and tick the matching checkboxes in
`.kiro/specs/coupon-service/tasks.md`. Any other file you touch must be called out in the
pull request under a heading `Out-of-scope changes`.

## Out of scope

- Cosmos, which is CS-23.
- HTTP, which is CS-15.

## Acceptance criteria

Defined in [`requirements.md`](../../.kiro/specs/coupon-service/requirements.md). Look each
one up and read the full text; do not infer it from the identifier. This ticket satisfies:

- **AC-1.1**
- **AC-1.2**
- **AC-1.6**
- **AC-3.6**

Each one needs a test that would fail without this change.

## Implementation notes

- Define ICouponValidator, IPriceCalculator, ICouponRedeemer, PolicyDecision, RejectionReason as a closed enum, and the repository ports, per design.md.
- PolicyDecision carries status, reason, plan, near-miss hint, optional trace and the policy content hash.
- Implement in-memory policy and redemption repositories with real ETag semantics: a write with a stale ETag must fail, so CAS logic is genuinely exercised.
- Implement the validate-then-price flow, and assert preview performs no writes at all.
- Test that a rejected coupon still returns a full-price breakdown, and that the content hash on the decision matches the evaluated policy.

## Verification

All of these must pass, with zero warnings. Do not suppress an analyzer to get there.

```powershell
dotnet build CouponService.slnx
dotnet test tests/CouponService.UnitTests --filter FullyQualifiedName~Application
dotnet test CouponService.slnx
```

## Prompt

Confirm CS-08 and CS-09 are merged, then paste this into a fresh Cursor
chat in this repository. Nothing else is needed: everything is either in the prompt or in a
file the prompt names.

```text
Implement ticket CS-12 in this repository, end to end.

Read these first, in order. They are the contract and they override anything you assume:
  1. .github/tickets/CS-12-application-services-and-in-memory-repositories.md
     This ticket: goal, scope, out of scope, acceptance criteria, implementation notes, verification.
  2. AGENTS.md
     Standing rules: money as decimal, engine purity, determinism via the injected IClock,
     scope discipline, warnings as errors, git conventions, PowerShell 5.1 environment.
  3. .kiro/specs/coupon-service/requirements.md
     The full text of AC-1.1, AC-1.2, AC-1.6, AC-3.6. Look each one up and read it. Do not
     infer an acceptance criterion from its identifier.
  4. .kiro/specs/coupon-service/design.md
     Decisions P-1 to P-12 and the exact type signatures. Overrides docs/solution-architecture.md
     where the two differ. Consult the architecture document for the reasoning behind a decision.

Then:
  1. Create branch ticket/CS-12-application-services-and-in-memory from the latest main.
  2. Implement the ticket, touching only these paths:
       src/CouponService.Application/
       src/CouponService.Infrastructure/InMemory/
       tests/CouponService.UnitTests/Application/
     Plus your own new test files, and the checkboxes in .kiro/specs/coupon-service/tasks.md.
     Do not reformat or tidy a file you did not otherwise need to change. If you genuinely
     need a file outside this list, change it and say so in the pull request.
  3. Add no NuGet package. If you believe one is required, stop and say why.
  4. Verify. Every command must pass, with zero warnings:
       dotnet build CouponService.slnx
       dotnet test tests/CouponService.UnitTests --filter FullyQualifiedName~Application
       dotnet test CouponService.slnx
     Each acceptance criterion needs a test that fails without your change. Prove that by
     reverting the change mentally, or temporarily, and confirming the test goes red.
  5. Tick the matching checkboxes in .kiro/specs/coupon-service/tasks.md.
  6. Commit in atomic, granular steps as you go - one logical change each, every commit
     building - with subjects of the form "CS-12: <imperative summary>". Do not squash the
     branch into a single commit; the granularity is what makes the pull request reviewable.
     Add no trailer of any kind: no Co-Authored-By, no Signed-off-by, no tool attribution.
  7. Push the branch and open a pull request against main titled "CS-12: Application services and in-memory repositories".
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
