# CS-26: Azure Pipelines definition, seeding and template linting

| | |
|---|---|
| **Wave** | 7 — Infrastructure and pipeline |
| **Size** | L |
| **Labels** | `wave-7, area:infra, blocked:azure, size:L` |
| **Blocked by** | [CS-22](CS-22-reqnroll-bdd-suite-with-a-configurable-target.md), [CS-25](CS-25-bicep-modules-for-the-whole-environment.md) |
| **Blocks** | — |

> **Read [`AGENTS.md`](../../AGENTS.md) before starting.** It carries the standing rules —
> money as `decimal`, engine purity, determinism via `IClock`, scope discipline, the definition
> of done and the git conventions — which are deliberately not repeated in every ticket.

## Goal

Define the eight-stage pipeline that deploys the whole solution from scratch with no manual steps, and prove the templates lint in CI without a subscription.

## Blocked by

- CS-22 — Reqnroll BDD suite with a configurable target
- CS-25 — Bicep modules for the whole environment

## Scope — touch only these paths

- `azure-pipelines.yml`
- `scripts/seed-policies.ps1`
- `.github/workflows/`

You may additionally add your own new test files, and tick the matching checkboxes in
`.kiro/specs/coupon-service/tasks.md`. Any other file you touch must be called out in the
pull request under a heading `Out-of-scope changes`.

## Out of scope

- Running the deployment stages, which needs a subscription and a service connection.

## Acceptance criteria

Defined in [`requirements.md`](../../.kiro/specs/coupon-service/requirements.md). Look each
one up and read the full text; do not infer it from the identifier. This ticket satisfies:

- **AC-9.1**
- **AC-9.2**
- **AC-9.3**
- **AC-9.4**
- **AC-9.5**
- **AC-9.6**

Each one needs a test that would fail without this change.

## Implementation notes

- Stages: build, test, package, provision, deploy, seed, BDD, verify, with the gates from section 18. Test failure must stop the pipeline before anything deploys.
- Authenticate with a workload-identity federated service connection. No secret in the pipeline, ever.
- Publish the Bicep what-if output as an artifact before applying changes.
- Idempotent seeding through the admin API for the deterministic policy set, safe to re-run.
- Add a GitHub Actions workflow running build, test and bicep lint on pull requests, so the repository has working CI even before Azure DevOps is wired up.
- Document the three one-time manual prerequisites and confirm nothing else is manual.

## Verification

All of these must pass, with zero warnings. Do not suppress an analyzer to get there.

```powershell
dotnet build CouponService.slnx
dotnet test CouponService.slnx
```

## Prompt

Confirm CS-22 and CS-25 are merged, then paste this into a fresh Cursor
chat in this repository. Nothing else is needed: everything is either in the prompt or in a
file the prompt names.

```text
Implement ticket CS-26 in this repository, end to end.

Read these first, in order. They are the contract and they override anything you assume:
  1. .github/tickets/CS-26-azure-pipelines-definition-seeding-and-template.md
     This ticket: goal, scope, out of scope, acceptance criteria, implementation notes, verification.
  2. AGENTS.md
     Standing rules: money as decimal, engine purity, determinism via the injected IClock,
     scope discipline, warnings as errors, git conventions, PowerShell 5.1 environment.
  3. .kiro/specs/coupon-service/requirements.md
     The full text of AC-9.1, AC-9.2, AC-9.3, AC-9.4, AC-9.5, AC-9.6. Look each one up and read it. Do not
     infer an acceptance criterion from its identifier.
  4. .kiro/specs/coupon-service/design.md
     Decisions P-1 to P-12 and the exact type signatures. Overrides docs/solution-architecture.md
     where the two differ. Consult the architecture document for the reasoning behind a decision.

Then:
  1. Create branch ticket/CS-26-azure-pipelines-definition-seeding from the latest main.
  2. Implement the ticket, touching only these paths:
       azure-pipelines.yml
       scripts/seed-policies.ps1
       .github/workflows/
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
  6. Commit as "CS-26: <imperative summary>", push the branch, and open a pull request titled
     "CS-26: Azure Pipelines definition, seeding and template linting".
     In the body, list every acceptance criterion satisfied, anything deliberately deferred,
     and any out-of-scope change you had to make.

Rules of engagement:
  - Do not suppress an analyzer, add NoWarn, or weaken a test to get a green build. Fix the code.
  - Do not stub an acceptance criterion and mark it done.
  - Never report a result you did not observe.
  - This ticket needs a live Azure subscription, which may not exist yet. Author, commit and
    lint the templates; do not claim a deployment happened. Say plainly what remains unverified.
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
