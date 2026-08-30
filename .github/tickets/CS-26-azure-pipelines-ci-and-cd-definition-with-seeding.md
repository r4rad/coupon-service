# CS-26: Azure Pipelines CI and CD definition with seeding

| | |
|---|---|
| **Wave** | 7 — Infrastructure and pipeline authored |
| **Size** | L |
| **Labels** | `wave-7, area:infra, size:L` |
| **Blocked by** | [CS-22](CS-22-reqnroll-bdd-suite-with-a-configurable-target.md), [CS-25](CS-25-bicep-modules-for-the-whole-environment.md) |
| **Blocks** | [CS-29](CS-29-multi-stage-pipelines-cd-develop-then-main.md) |

> **Read [`AGENTS.md`](../../AGENTS.md) before starting.** It carries the standing rules —
> money as `decimal`, engine purity, determinism via `IClock`, scope discipline, the definition
> of done and the git conventions — which are deliberately not repeated in every ticket.

## Goal

Define a single Azure Pipelines YAML that is both CI and CD (P-13/P-14). Author the eight-stage skeleton and seed script; CS-29 finishes multi-branch wiring and the green live runs. No GitHub Actions.

## Blocked by

- CS-22 — Reqnroll BDD suite with a configurable target
- CS-25 — Bicep modules for the whole environment

## Scope — touch only these paths

- `azure-pipelines.yml`
- `scripts/seed-policies.ps1`
- `docs/pipeline-prerequisites.md`

You may additionally add your own new test files, and tick the matching checkboxes in
`.kiro/specs/coupon-service/tasks.md`. Any other file you touch must be called out in the
pull request under a heading `Out-of-scope changes`.

## Out of scope

- Creating the Azure DevOps project or service connection (one-time manual steps documented in docs/pipeline-prerequisites.md).
- A green full CD run against the live subscription, dual-environment params, and branch-to-RG mapping. That is CS-29.
- Any GitHub Actions workflow. Azure Pipelines is the only CI and CD system.

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

- Single azure-pipelines.yml with eight stages matching section 18: build, test, package, provision, deploy, seed, BDD, verify.
- Target branch model (P-14): PR into develop or main = CI only (build, test, bicep build/lint). Merge to develop or main (or Manual) = full CD. Do not provision or deploy on PRs.
- Auth: workload-identity federated ARM service connection only. No client secret in YAML or variable group (AC-9.3).
- Provision stage: publish az deployment group what-if as a pipeline artifact, then apply. Resource group and param file come from pipeline variables/parameters - never hard-code secrets or subscription IDs.
- Idempotent scripts/seed-policies.ps1 through the admin API for the deterministic policy set, safe to re-run (AC-9.5, AC-9.6).
- Document one-time prerequisites in docs/pipeline-prerequisites.md: private Azure DevOps project, WIF connection, Entra permission, User Access Administrator on target RGs (Key Vault RBAC assignments), and soft-delete / name-salt notes. Confirm nothing else is manual after that.

## Verification

All of these must pass, with zero warnings. Do not suppress an analyzer to get there.

```powershell
dotnet build CouponService.slnx
dotnet test CouponService.slnx
az bicep build --file infra/bicep/main.bicep --stdout
```

## Prompt

Confirm CS-22 and CS-25 are merged, then paste this into a fresh Cursor
chat in this repository. Nothing else is needed: everything is either in the prompt or in a
file the prompt names.

```text
Implement ticket CS-26 in this repository, end to end.

Read these first, in order. They are the contract and they override anything you assume:
  1. .github/tickets/CS-26-azure-pipelines-ci-and-cd-definition-with-seeding.md
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
  1. Create branch ticket/CS-26-azure-pipelines-ci-and-cd-definition from the latest main.
  2. Implement the ticket, touching only these paths:
       azure-pipelines.yml
       scripts/seed-policies.ps1
       docs/pipeline-prerequisites.md
     Plus your own new test files, and the checkboxes in .kiro/specs/coupon-service/tasks.md.
     Do not reformat or tidy a file you did not otherwise need to change. If you genuinely
     need a file outside this list, change it and say so in the pull request.
  3. Add no NuGet package. If you believe one is required, stop and say why.
  4. Verify. Every command must pass, with zero warnings:
       dotnet build CouponService.slnx
       dotnet test CouponService.slnx
       az bicep build --file infra/bicep/main.bicep --stdout
     Each acceptance criterion needs a test that fails without your change. Prove that by
     reverting the change mentally, or temporarily, and confirming the test goes red.
  5. Tick the matching checkboxes in .kiro/specs/coupon-service/tasks.md.
  6. Commit in atomic, granular steps as you go - one logical change each, every commit
     building - with subjects of the form "CS-26: <imperative summary>". Do not squash the
     branch into a single commit; the granularity is what makes the pull request reviewable.
     Add no trailer of any kind: no Co-Authored-By, no Signed-off-by, no tool attribution.
  7. Push the branch and open a pull request titled "CS-26: Azure Pipelines CI and CD definition with seeding".
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
