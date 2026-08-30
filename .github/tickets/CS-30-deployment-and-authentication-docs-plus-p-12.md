# CS-30: Deployment and authentication docs plus P-12 correction

| | |
|---|---|
| **Wave** | 8 — Live Azure provision, Entra, CD run, docs |
| **Size** | M |
| **Labels** | `wave-8, area:infra, size:M` |
| **Blocked by** | [CS-29](CS-29-multi-stage-pipelines-cd-develop-then-main.md) |
| **Blocks** | — |

> **Read [`AGENTS.md`](../../AGENTS.md) before starting.** It carries the standing rules —
> money as `decimal`, engine purity, determinism via `IClock`, scope discipline, the definition
> of done and the git conventions — which are deliberately not repeated in every ticket.

## Goal

Leave the reviewer with clear multi-stage deployment and authentication write-ups, and correct the APIM cache claim per decision P-12.

## Blocked by

- CS-29 — Multi-stage Pipelines CD (develop then main) and green runs

## Scope — touch only these paths

- `docs/deployment.md`
- `docs/authentication.md`
- `docs/pipeline-prerequisites.md`
- `docs/solution-architecture.md`
- `docs/assumptions.md`
- `README.md`

You may additionally add your own new test files, and tick the matching checkboxes in
`.kiro/specs/coupon-service/tasks.md`. Any other file you touch must be called out in the
pull request under a heading `Out-of-scope changes`.

## Out of scope

- Further infrastructure or pipeline behaviour changes unless a doc reveals a factual error, which must be fixed and called out under Out-of-scope changes.
- React SPA (optional stretch, not this ticket).
- Merging develop into main. The operator does that after review.

## Acceptance criteria

Defined in [`requirements.md`](../../.kiro/specs/coupon-service/requirements.md). Look each
one up and read the full text; do not infer it from the identifier. This ticket satisfies:

- **AC-9.1**
- **AC-9.7**
- **AC-7.6**
- **AC-7.7**

Each one needs a test that would fail without this change.

## Implementation notes

- Git workflow: branch from latest develop; open the PR against develop (not main). Operator merges develop to main separately.
- Complete docs/deployment.md: feature->develop->main flow, empty RG to green pipeline for each environment, what-if, teardown, SKUs, soft-delete/purge-protection naming, link to azure-pipelines.yml and param files.
- Complete docs/authentication.md: Entra apps, roles, APIM validate-jwt, managed identity hop, local test-token guard behaviour.
- Align docs/pipeline-prerequisites.md with the live CS-29 setup (private project, WIF, UAA on both RGs, branch policies).
- Apply P-12: edit docs/solution-architecture.md so it no longer claims APIM Consumption response caching; document Static Web Apps CDN plus backend ETag and Cache-Control instead. Document P-13/P-14 pipeline branching in the architecture or assumptions.
- Write docs/assumptions.md for currency, region, SKUs, private ADO projects (no public for new orgs), dual RGs, and deferred work (simulate, shadow, SPA).
- Point README.md at these docs so a reviewer finds them in one hop.

## Verification

All of these must pass, with zero warnings. Do not suppress an analyzer to get there.

```powershell
dotnet build CouponService.slnx
dotnet test CouponService.slnx
```

## Prompt

Confirm CS-29 is merged, then paste this into a fresh Cursor
chat in this repository. Nothing else is needed: everything is either in the prompt or in a
file the prompt names.

```text
Implement ticket CS-30 in this repository, end to end.

Read these first, in order. They are the contract and they override anything you assume:
  1. .github/tickets/CS-30-deployment-and-authentication-docs-plus-p-12.md
     This ticket: goal, scope, out of scope, acceptance criteria, implementation notes, verification.
  2. AGENTS.md
     Standing rules: money as decimal, engine purity, determinism via the injected IClock,
     scope discipline, warnings as errors, git conventions, PowerShell 5.1 environment.
  3. .kiro/specs/coupon-service/requirements.md
     The full text of AC-9.1, AC-9.7, AC-7.6, AC-7.7. Look each one up and read it. Do not
     infer an acceptance criterion from its identifier.
  4. .kiro/specs/coupon-service/design.md
     Decisions P-1 to P-12 and the exact type signatures. Overrides docs/solution-architecture.md
     where the two differ. Consult the architecture document for the reasoning behind a decision.

Then:
  1. Create branch ticket/CS-30-deployment-and-authentication-docs-plus from the latest develop.
  2. Implement the ticket, touching only these paths:
       docs/deployment.md
       docs/authentication.md
       docs/pipeline-prerequisites.md
       docs/solution-architecture.md
       docs/assumptions.md
       README.md
     Plus your own new test files, and the checkboxes in .kiro/specs/coupon-service/tasks.md.
     Do not reformat or tidy a file you did not otherwise need to change. If you genuinely
     need a file outside this list, change it and say so in the pull request.
  3. Add no NuGet package. If you believe one is required, stop and say why.
  4. Verify. Every command must pass, with zero warnings:
       dotnet build CouponService.slnx
       dotnet test CouponService.slnx
     Each acceptance criterion needs a test that fails without your change. Prove that by
     reverting the change mentally, or temporarily, and confirming the test goes red.
  5. Tick the matching checkboxes in .kiro/specs/coupon-service/tasks.md.
  6. Commit in atomic, granular steps as you go - one logical change each, every commit
     building - with subjects of the form "CS-30: <imperative summary>". Do not squash the
     branch into a single commit; the granularity is what makes the pull request reviewable.
     Add no trailer of any kind: no Co-Authored-By, no Signed-off-by, no tool attribution.
  7. Push the branch and open a pull request against develop titled "CS-30: Deployment and authentication docs plus P-12 correction".
     Do not open the PR against main. The operator merges develop to main separately after
     the develop CD path is green.
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
