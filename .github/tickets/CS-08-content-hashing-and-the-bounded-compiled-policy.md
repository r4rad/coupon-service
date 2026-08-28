# CS-08: Content hashing and the bounded compiled-policy cache

| | |
|---|---|
| **Wave** | 1 — Policy engine core |
| **Size** | S |
| **Labels** | `wave-1, area:engine, size:S` |
| **Blocked by** | [CS-07](CS-07-compiler-with-cost-ordering-and-the-evaluation.md) |
| **Blocks** | [CS-12](CS-12-application-services-and-in-memory-repositories.md) |

> **Read [`AGENTS.md`](../../AGENTS.md) before starting.** It carries the standing rules —
> money as `decimal`, engine purity, determinism via `IClock`, scope discipline, the definition
> of done and the git conventions — which are deliberately not repeated in every ticket.

## Goal

Parse and compile once, execute many, keyed on the policy's content so an edit invalidates itself with no explicit cache call.

## Blocked by

- CS-07 — Compiler with cost ordering, and the evaluation scope

## Scope — touch only these paths

- `src/CouponService.Engine/Caching/`
- `tests/CouponService.EngineTests/Caching/`

You may additionally add your own new test files, and tick the matching checkboxes in
`.kiro/specs/coupon-service/tasks.md`. Any other file you touch must be called out in the
pull request under a heading `Out-of-scope changes`.

## Out of scope

- Distributed caching. In-process only, deliberately.

## Acceptance criteria

Defined in [`requirements.md`](../../.kiro/specs/coupon-service/requirements.md). Look each
one up and read the full text; do not infer it from the identifier. This ticket satisfies:

- **AC-3.4**
- **AC-3.5**
- **AC-3.6**
- **NFR-4**

Each one needs a test that would fail without this change.

## Implementation notes

- Canonicalise the policy JSON (ordered properties, normalised number formatting) then SHA-256 it. Two documents differing only in key order or whitespace must hash identically.
- Cache compiled policies on that hash with a bounded size and sliding expiry, so neither memory nor coupon-code enumeration is unbounded.
- Cache negative lookups briefly, to blunt enumeration of non-existent codes.
- Test that recompilation happens exactly once for repeated evaluation, that changing one threshold produces a different hash and a recompile, and that the cache evicts under its size bound.

## Verification

All of these must pass, with zero warnings. Do not suppress an analyzer to get there.

```powershell
dotnet build CouponService.slnx
dotnet test tests/CouponService.EngineTests --filter FullyQualifiedName~Caching
dotnet test CouponService.slnx
```

## Prompt

Confirm CS-07 is merged, then paste this into a fresh Cursor
chat in this repository. Nothing else is needed: everything is either in the prompt or in a
file the prompt names.

```text
Implement ticket CS-08 in this repository, end to end.

Read these first, in order. They are the contract and they override anything you assume:
  1. .github/tickets/CS-08-content-hashing-and-the-bounded-compiled-policy.md
     This ticket: goal, scope, out of scope, acceptance criteria, implementation notes, verification.
  2. AGENTS.md
     Standing rules: money as decimal, engine purity, determinism via the injected IClock,
     scope discipline, warnings as errors, git conventions, PowerShell 5.1 environment.
  3. .kiro/specs/coupon-service/requirements.md
     The full text of AC-3.4, AC-3.5, AC-3.6, NFR-4. Look each one up and read it. Do not
     infer an acceptance criterion from its identifier.
  4. .kiro/specs/coupon-service/design.md
     Decisions P-1 to P-12 and the exact type signatures. Overrides docs/solution-architecture.md
     where the two differ. Consult the architecture document for the reasoning behind a decision.

Then:
  1. Create branch ticket/CS-08-content-hashing-and-the-bounded from the latest main.
  2. Implement the ticket, touching only these paths:
       src/CouponService.Engine/Caching/
       tests/CouponService.EngineTests/Caching/
     Plus your own new test files, and the checkboxes in .kiro/specs/coupon-service/tasks.md.
     Do not reformat or tidy a file you did not otherwise need to change. If you genuinely
     need a file outside this list, change it and say so in the pull request.
  3. Add no NuGet package. If you believe one is required, stop and say why.
  4. Verify. Every command must pass, with zero warnings:
       dotnet build CouponService.slnx
       dotnet test tests/CouponService.EngineTests --filter FullyQualifiedName~Caching
       dotnet test CouponService.slnx
     Each acceptance criterion needs a test that fails without your change. Prove that by
     reverting the change mentally, or temporarily, and confirming the test goes red.
  5. Tick the matching checkboxes in .kiro/specs/coupon-service/tasks.md.
  6. Commit as "CS-08: <imperative summary>", push the branch, and open a pull request titled
     "CS-08: Content hashing and the bounded compiled-policy cache".
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
