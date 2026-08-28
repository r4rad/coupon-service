# Agent working agreement

Read this before touching code. It is the standing contract for every autonomous ticket run, so ticket bodies stay short and only describe what is specific to them.

## What this repository is

A **Coupon Service** for a pizza ordering platform: a policy engine that treats coupon rules as data, a redemption lifecycle that enforces usage caps under concurrency, and the infrastructure to deploy it. Built as a technical assignment, so the code is graded on judgement, not just function.

Authoritative documents, in precedence order:

1. `.kiro/specs/coupon-service/requirements.md` — numbered acceptance criteria (`AC-x.y`). **These define done.**
2. `.kiro/specs/coupon-service/design.md` — planning decisions (`P-1`…`P-12`) and exact type signatures. Where this contradicts the architecture document, **this wins**.
3. `docs/solution-architecture.md` — the full architecture and the reasoning behind it.
4. `.kiro/specs/coupon-service/tasks.md` — the task breakdown these tickets came from.

If a ticket seems to conflict with the specs, the specs win. Say so in the pull request rather than silently choosing.

## Non-negotiable rules

**Money.** `decimal` everywhere. Never `double` or `float`. Round once, at the point a monetary value is produced, two decimal places, `MidpointRounding.AwayFromZero`.

**Determinism.** Never call `DateTime.Now`, `DateTime.UtcNow` or `DateTimeOffset.Now` in domain, engine or application code. Take time from the injected `IClock`. Evaluation must be reproducible.

**Engine purity.** `CouponService.Engine` may reference `CouponService.Domain` and the base class library, and nothing else. No ASP.NET Core, no `Microsoft.Azure.Cosmos`, no Azure SDK, no `IConfiguration`. A test enforces this — if it fails, the design is wrong, not the test.

**No secrets.** Never commit connection strings, keys, tokens or `appsettings.*.Local.json`. Local secrets go in `dotnet user-secrets`. If a ticket needs a value you do not have, use a placeholder and say so in the pull request.

**Warnings are errors.** Do not add `#pragma warning disable`, `<NoWarn>` or a `.editorconfig` severity downgrade to make a build pass. Fix the code. If a rule is genuinely wrong for this codebase, change `.editorconfig` in a separate commit with the reason in the message.

**Policy data is never code.** The engine must expose no way to execute a string supplied in a policy document — no scripting, no compiled expressions from text, no regular expressions from policy data. This is a security property, not a style preference.

## Scope discipline

Tickets are designed to run in parallel, so staying inside your lane prevents conflicts.

- Touch only the paths listed in the ticket's `scope`.
- You may always additionally edit: your own new test files, and the checkboxes in `.kiro/specs/coupon-service/tasks.md`.
- Do **not** reformat, rename or "tidy" files you did not otherwise need to change.
- Do **not** add a NuGet package that is not named in the ticket without saying why in the pull request.
- If completing the ticket genuinely requires a file outside scope, make the change, and call it out explicitly in the pull request under a heading `Out-of-scope changes`.

## Definition of done

Every ticket, without exception:

1. `dotnet build CouponService.slnx` succeeds with zero warnings.
2. The ticket's `verify` commands all pass.
3. `dotnet test CouponService.slnx` shows no regressions in previously passing tests.
4. Each acceptance criterion listed on the ticket is satisfied, and covered by a test that would fail without your change.
5. The corresponding checkboxes in `tasks.md` are ticked.
6. A pull request exists, describing what changed and which `AC-` items it satisfies.

A ticket is not done because the code looks right. It is done when a test proves it.

## Testing expectations

- Unit, engine and pricing tests must need **no** database, emulator, container or network. This is acceptance criterion AC-10.5 and it protects the inner loop.
- Test names state behaviour, not method names. `Cap_rescales_allocations_so_they_sum_to_the_capped_total` over `TestCap2`.
- Prove claims rather than asserting them. Where the design claims work is avoided, instrument it — for example, a counting fact provider that must be invoked zero times when a cheap predicate fails.
- Cover the boundary: empty basket, single line, zero quantity, discount larger than the basket, cap of exactly zero, a policy that matches nothing.

## Git conventions

- Branch: `ticket/CS-XX-short-slug`, created from the latest `main`.
- Commit subject: `CS-XX: imperative summary`, for example `CS-04: parse condition grammar with node budget`.
- Small, coherent commits. Do not squash unrelated work into one.
- Never force-push a shared branch. Never commit directly to `main`.
- Pull request title: `CS-XX: <ticket title>`. Body must list acceptance criteria satisfied and anything deliberately deferred.

## When you are blocked

Stop. Do not invent scope, stub out an acceptance criterion, or weaken a test to get green.

Write what you tried, what blocked you, and the options you see — into the pull request if one exists, otherwise as a comment on the issue. A ticket returned with a clear blocker is a good outcome; a ticket returned green with a hollowed-out test is not.

## Environment notes

- Windows, PowerShell **5.1**. `&&` chaining does not work; use `;` between commands. `??` and `Join-String` are unavailable.
- Save any `.ps1` file as **UTF-8 with BOM**. Without the BOM, PowerShell 5.1 decodes the file as Windows-1252, and a character such as an em dash becomes three bytes ending in `0x94` — a closing curly quote, which silently terminates a double-quoted string and produces baffling parse errors far from the real line. Either add the BOM or keep the file ASCII-only.
- Ticket specifications live in `.github/tickets/*.md` and are **generated** from `.github/tickets.json`. Edit the JSON and run `./scripts/generate-ticket-docs.ps1`; never hand-edit a generated ticket file.
- Solution file is `CouponService.slnx`, the .NET 10 XML format, not `.sln`.
- Target framework `net10.0`, pinned by `global.json`.
- The Azure subscription may not exist yet. Anything requiring a live Azure resource is authored, committed and linted, but not expected to run. Never fake a deployment result.
