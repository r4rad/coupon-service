# CS-25: Bicep modules for the whole environment

| | |
|---|---|
| **Wave** | 7 — Infrastructure and pipeline |
| **Size** | L |
| **Labels** | `wave-7, area:infra, blocked:azure, size:L` |
| **Blocked by** | [CS-01](CS-01-solution-wiring-central-packages-and-build-gates.md) |
| **Blocks** | [CS-26](CS-26-azure-pipelines-definition-seeding-and-template.md) |

> **Read [`AGENTS.md`](../../AGENTS.md) before starting.** It carries the standing rules —
> money as `decimal`, engine purity, determinism via `IClock`, scope discipline, the definition
> of done and the git conventions — which are deliberately not repeated in every ticket.

## Goal

Author infrastructure that provisions everything from an empty resource group. Cannot be deployed until a subscription exists, so correctness is established by build and lint.

## Blocked by

- CS-01 — Solution wiring, central packages and build gates

## Scope — touch only these paths

- `infra/bicep/`

You may additionally add your own new test files, and tick the matching checkboxes in
`.kiro/specs/coupon-service/tasks.md`. Any other file you touch must be called out in the
pull request under a heading `Out-of-scope changes`.

## Out of scope

- Actually deploying. Never report a deployment that did not happen.

## Acceptance criteria

Defined in [`requirements.md`](../../.kiro/specs/coupon-service/requirements.md). Look each
one up and read the full text; do not infer it from the identifier. This ticket satisfies:

- **AC-9.1**
- **AC-9.2**
- **NFR-6**

Each one needs a test that would fail without this change.

## Implementation notes

- Modules: observability, identity, keyvault, cosmos, containerapps, apim, apim-api, staticwebapp, and an appservice fallback.
- main.bicep composing them, with main.demo.bicepparam carrying no secrets.
- Pin the free and near-free SKUs from section 17: APIM Consumption, Container Apps consumption, Static Web Apps Free, Cosmos serverless with a free-tier switch, Log Analytics with a daily cap.
- Container Apps are created with a public placeholder image, per decision P-11, so the first deploy into an empty resource group does not deadlock on a registry that has no image yet.
- Tag every resource with project, env and owner so cost can be filtered and the environment deleted in one command.
- Verify with bicep build and bicep lint. State plainly in the pull request that no deployment has been attempted.

## Verification

All of these must pass, with zero warnings. Do not suppress an analyzer to get there.

```powershell
dotnet build CouponService.slnx
az bicep build --file infra/bicep/main.bicep --stdout
dotnet test CouponService.slnx
```

## Prompt

Confirm CS-01 is merged, then paste this into a fresh Cursor
chat in this repository. Nothing else is needed: everything is either in the prompt or in a
file the prompt names.

```text
Implement ticket CS-25 in this repository, end to end.

Read these first, in order. They are the contract and they override anything you assume:
  1. .github/tickets/CS-25-bicep-modules-for-the-whole-environment.md
     This ticket: goal, scope, out of scope, acceptance criteria, implementation notes, verification.
  2. AGENTS.md
     Standing rules: money as decimal, engine purity, determinism via the injected IClock,
     scope discipline, warnings as errors, git conventions, PowerShell 5.1 environment.
  3. .kiro/specs/coupon-service/requirements.md
     The full text of AC-9.1, AC-9.2, NFR-6. Look each one up and read it. Do not
     infer an acceptance criterion from its identifier.
  4. .kiro/specs/coupon-service/design.md
     Decisions P-1 to P-12 and the exact type signatures. Overrides docs/solution-architecture.md
     where the two differ. Consult the architecture document for the reasoning behind a decision.

Then:
  1. Create branch ticket/CS-25-bicep-modules-for-the-whole-environment from the latest main.
  2. Implement the ticket, touching only these paths:
       infra/bicep/
     Plus your own new test files, and the checkboxes in .kiro/specs/coupon-service/tasks.md.
     Do not reformat or tidy a file you did not otherwise need to change. If you genuinely
     need a file outside this list, change it and say so in the pull request.
  3. Add no NuGet package. If you believe one is required, stop and say why.
  4. Verify. Every command must pass, with zero warnings:
       dotnet build CouponService.slnx
       az bicep build --file infra/bicep/main.bicep --stdout
       dotnet test CouponService.slnx
     Each acceptance criterion needs a test that fails without your change. Prove that by
     reverting the change mentally, or temporarily, and confirming the test goes red.
  5. Tick the matching checkboxes in .kiro/specs/coupon-service/tasks.md.
  6. Commit in atomic, granular steps as you go - one logical change each, every commit
     building - with subjects of the form "CS-25: <imperative summary>". Do not squash the
     branch into a single commit; the granularity is what makes the pull request reviewable.
     Add no trailer of any kind: no Co-Authored-By, no Signed-off-by, no tool attribution.
  7. Push the branch and open a pull request titled "CS-25: Bicep modules for the whole environment".
     In the body, list every acceptance criterion satisfied, anything deliberately deferred,
     and any out-of-scope change you had to make.

Rules of engagement:
  - Do not suppress an analyzer, add NoWarn, or weaken a test to get a green build. Fix the code.
  - Do not stub an acceptance criterion and mark it done.
  - Never report a result you did not observe.
  - Comment only what the code cannot express: an invariant, an external constraint, a
    non-obvious trade-off, or the AC or P item that forces a behaviour. Do not narrate the
    code, and never explain your change in a comment - that belongs in the commit message.
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
6. The branch is a sequence of atomic commits, none of them carrying a trailer.
7. A pull request exists listing the acceptance criteria satisfied and anything deferred.

---

<sub>Generated from `.github/tickets.json` by `scripts/generate-ticket-docs.ps1`. Edit the JSON, then regenerate.</sub>
