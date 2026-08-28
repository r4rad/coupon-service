# CS-17: Admin policy API and manifest endpoint

| | |
|---|---|
| **Wave** | 4 — API, auth, logging |
| **Size** | M |
| **Labels** | `wave-4, area:api, size:M` |
| **Blocked by** | [CS-15](CS-15-api-host-preview-endpoint-and-health-probes.md) |
| **Blocks** | [CS-18](CS-18-authentication-roles-and-the-test-token-startup.md) |

> **Read [`AGENTS.md`](../../AGENTS.md) before starting.** It carries the standing rules —
> money as `decimal`, engine purity, determinism via `IClock`, scope discipline, the definition
> of done and the git conventions — which are deliberately not repeated in every ticket.

## Goal

Let a campaign manager create and govern policies through an API, validated against the manifest before anything is stored.

## Blocked by

- CS-15 — API host, preview endpoint and health probes

## Scope — touch only these paths

- `src/CouponService.Api/Controllers/`
- `tests/CouponService.ApiTests/Admin/`

You may additionally add your own new test files, and tick the matching checkboxes in
`.kiro/specs/coupon-service/tasks.md`. Any other file you touch must be called out in the
pull request under a heading `Out-of-scope changes`.

## Out of scope

- Simulate and shadow mode, which are deferred to the post-Azure wave.
- Any admin UI.

## Acceptance criteria

Defined in [`requirements.md`](../../.kiro/specs/coupon-service/requirements.md). Look each
one up and read the full text; do not infer it from the identifier. This ticket satisfies:

- **AC-6.1**
- **AC-6.2**
- **AC-6.3**
- **AC-6.4**
- **AC-2.3**

Each one needs a test that would fail without this change.

## Implementation notes

- GET and POST /v1/admin/policies, GET, PUT and DELETE /v1/admin/policies/{id}.
- Validate every write against the engine manifest and return 400 listing all offending nodes, so an invalid policy can never reach the request path.
- PUT requires a matching ETag and returns 412 on a stale write.
- DELETE transitions to Archived and never removes the document, because historical orders must stay explainable.
- GET /v1/policy-engine/manifest returns the generated manifest.
- Test the 400 body shape for an unknown fact, the 412 path, and that a deleted policy is still readable as Archived.

## Verification

All of these must pass, with zero warnings. Do not suppress an analyzer to get there.

```powershell
dotnet build CouponService.slnx
dotnet test tests/CouponService.ApiTests --filter FullyQualifiedName~Admin
dotnet test CouponService.slnx
```

## Prompt

Confirm CS-15 is merged, then paste this into a fresh Cursor
chat in this repository. Nothing else is needed: everything is either in the prompt or in a
file the prompt names.

```text
Implement ticket CS-17 in this repository, end to end.

Read these first, in order. They are the contract and they override anything you assume:
  1. .github/tickets/CS-17-admin-policy-api-and-manifest-endpoint.md
     This ticket: goal, scope, out of scope, acceptance criteria, implementation notes, verification.
  2. AGENTS.md
     Standing rules: money as decimal, engine purity, determinism via the injected IClock,
     scope discipline, warnings as errors, git conventions, PowerShell 5.1 environment.
  3. .kiro/specs/coupon-service/requirements.md
     The full text of AC-6.1, AC-6.2, AC-6.3, AC-6.4, AC-2.3. Look each one up and read it. Do not
     infer an acceptance criterion from its identifier.
  4. .kiro/specs/coupon-service/design.md
     Decisions P-1 to P-12 and the exact type signatures. Overrides docs/solution-architecture.md
     where the two differ. Consult the architecture document for the reasoning behind a decision.

Then:
  1. Create branch ticket/CS-17-admin-policy-api-and-manifest-endpoint from the latest main.
  2. Implement the ticket, touching only these paths:
       src/CouponService.Api/Controllers/
       tests/CouponService.ApiTests/Admin/
     Plus your own new test files, and the checkboxes in .kiro/specs/coupon-service/tasks.md.
     Do not reformat or tidy a file you did not otherwise need to change. If you genuinely
     need a file outside this list, change it and say so in the pull request.
  3. Add no NuGet package. If you believe one is required, stop and say why.
  4. Verify. Every command must pass, with zero warnings:
       dotnet build CouponService.slnx
       dotnet test tests/CouponService.ApiTests --filter FullyQualifiedName~Admin
       dotnet test CouponService.slnx
     Each acceptance criterion needs a test that fails without your change. Prove that by
     reverting the change mentally, or temporarily, and confirming the test goes red.
  5. Tick the matching checkboxes in .kiro/specs/coupon-service/tasks.md.
  6. Commit in atomic, granular steps as you go - one logical change each, every commit
     building - with subjects of the form "CS-17: <imperative summary>". Do not squash the
     branch into a single commit; the granularity is what makes the pull request reviewable.
     Add no trailer of any kind: no Co-Authored-By, no Signed-off-by, no tool attribution.
  7. Push the branch and open a pull request titled "CS-17: Admin policy API and manifest endpoint".
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
