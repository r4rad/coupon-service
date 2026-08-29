# CS-29: Wire Azure Pipelines CD and prove a green full run

| | |
|---|---|
| **Wave** | 8 — Live Azure provision, Entra, CD run, docs |
| **Size** | L |
| **Labels** | `wave-8, area:infra, area:test, size:L` |
| **Blocked by** | [CS-26](CS-26-azure-pipelines-ci-and-cd-definition-with-seeding.md), [CS-28](CS-28-entra-apps-apim-jwt-policies-and-managed-identity.md) |
| **Blocks** | [CS-30](CS-30-deployment-and-authentication-docs-plus-p-12.md) |

> **Read [`AGENTS.md`](../../AGENTS.md) before starting.** It carries the standing rules —
> money as `decimal`, engine purity, determinism via `IClock`, scope discipline, the definition
> of done and the git conventions — which are deliberately not repeated in every ticket.

## Goal

Connect azure-pipelines.yml to Azure DevOps with a WIF service connection and obtain one green eight-stage run: provision, deploy, seed, BDD through APIM, verify.

## Blocked by

- CS-26 — Azure Pipelines CI and CD definition with seeding
- CS-28 — Entra apps, APIM JWT policies and managed identity hop

## Scope — touch only these paths

- `azure-pipelines.yml`
- `scripts/seed-policies.ps1`
- `tests/CouponService.Bdd/`
- `docs/pipeline-prerequisites.md`
- `docs/deployment.md`

You may additionally add your own new test files, and tick the matching checkboxes in
`.kiro/specs/coupon-service/tasks.md`. Any other file you touch must be called out in the
pull request under a heading `Out-of-scope changes`.

## Out of scope

- Authoring Bicep from scratch (CS-25) or first manual provision (CS-27).
- GitHub Actions. Do not add any.
- simulate, shadow, alerts workbook, React SPA.

## Acceptance criteria

Defined in [`requirements.md`](../../.kiro/specs/coupon-service/requirements.md). Look each
one up and read the full text; do not infer it from the identifier. This ticket satisfies:

- **AC-9.1**
- **AC-9.3**
- **AC-9.4**
- **AC-9.5**
- **AC-9.6**
- **AC-10.1**

Each one needs a test that would fail without this change.

## Implementation notes

- Complete the documented one-time prerequisites if not already done: public or suitably licensed Azure DevOps project, WIF service connection to the subscription or rg-coupon-demo, pipeline pointing at azure-pipelines.yml.
- Set pipeline variables for resource group, location, and service connection name. No secrets in the YAML file.
- Run the full CD path on main or via manual run. If a stage fails, fix in atomic commits and re-run until green. Capture the run URL in the pull request.
- Point the BDD suite at the APIM gateway base URL with the configured token strategy so the same features from CS-22 run through the gateway (AC-10.1).
- Re-run the pipeline once to prove seed and provision are idempotent (AC-9.6).
- Update docs/deployment.md with the observed stage order and how to trigger a run.

## Verification

All of these must pass, with zero warnings. Do not suppress an analyzer to get there.

```powershell
dotnet build CouponService.slnx
dotnet test CouponService.slnx
```

## Prompt

Confirm CS-26 and CS-28 are merged, then paste this into a fresh Cursor
chat in this repository. Nothing else is needed: everything is either in the prompt or in a
file the prompt names.

```text
Implement ticket CS-29 in this repository, end to end.

Read these first, in order. They are the contract and they override anything you assume:
  1. .github/tickets/CS-29-wire-azure-pipelines-cd-and-prove-a-green-full-run.md
     This ticket: goal, scope, out of scope, acceptance criteria, implementation notes, verification.
  2. AGENTS.md
     Standing rules: money as decimal, engine purity, determinism via the injected IClock,
     scope discipline, warnings as errors, git conventions, PowerShell 5.1 environment.
  3. .kiro/specs/coupon-service/requirements.md
     The full text of AC-9.1, AC-9.3, AC-9.4, AC-9.5, AC-9.6, AC-10.1. Look each one up and read it. Do not
     infer an acceptance criterion from its identifier.
  4. .kiro/specs/coupon-service/design.md
     Decisions P-1 to P-12 and the exact type signatures. Overrides docs/solution-architecture.md
     where the two differ. Consult the architecture document for the reasoning behind a decision.

Then:
  1. Create branch ticket/CS-29-wire-azure-pipelines-cd-and-prove from the latest main.
  2. Implement the ticket, touching only these paths:
       azure-pipelines.yml
       scripts/seed-policies.ps1
       tests/CouponService.Bdd/
       docs/pipeline-prerequisites.md
       docs/deployment.md
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
     building - with subjects of the form "CS-29: <imperative summary>". Do not squash the
     branch into a single commit; the granularity is what makes the pull request reviewable.
     Add no trailer of any kind: no Co-Authored-By, no Signed-off-by, no tool attribution.
  7. Push the branch and open a pull request titled "CS-29: Wire Azure Pipelines CD and prove a green full run".
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
