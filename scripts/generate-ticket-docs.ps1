<#
.SYNOPSIS
    Renders one markdown file per ticket, plus an index, from .github/tickets.json.

.DESCRIPTION
    .github/tickets.json stays the single source of truth. These markdown files are
    generated artefacts: they are what a human reads, what gh-bootstrap.ps1 uploads as the
    GitHub issue body, and what an autonomous agent is pointed at. Regenerate after any
    edit to tickets.json rather than editing a ticket file by hand.

.EXAMPLE
    ./scripts/generate-ticket-docs.ps1
    ./scripts/generate-ticket-docs.ps1 -Check
#>
[CmdletBinding()]
param(
    # Fail instead of writing when the generated output would differ from what is on disk.
    # Intended for CI, so a stale ticket file cannot be merged.
    [switch] $Check
)

$ErrorActionPreference = 'Stop'
$repoRoot   = Split-Path -Parent $PSScriptRoot
$ticketFile = Join-Path $repoRoot '.github/tickets.json'
$outDir     = Join-Path $repoRoot '.github/tickets'

if (-not (Test-Path $ticketFile)) { throw "Not found: $ticketFile" }
$spec = Get-Content $ticketFile -Raw | ConvertFrom-Json

if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

# --- Helpers ------------------------------------------------------------------

function Get-Slug {
    param([string]$Text, [int]$MaxLength = 50)
    $s = ($Text -replace '[^a-zA-Z0-9]+', '-').Trim('-').ToLowerInvariant()
    if ($s.Length -gt $MaxLength) {
        $s = $s.Substring(0, $MaxLength)
        # Cut back to the last word boundary, so a branch or file name never ends mid-word.
        $lastDash = $s.LastIndexOf('-')
        if ($lastDash -gt ($MaxLength / 2)) { $s = $s.Substring(0, $lastDash) }
        # Then drop a trailing connective, which reads as though the name were cut off.
        $s = $s -replace '-(and|the|a|an|for|with|in|of|to|from|on|by)$', ''
        $s = $s.Trim('-')
    }
    return $s
}

function Get-WaveName {
    param($Spec, [int]$Wave)
    $label = $Spec.labels | Where-Object { $_.name -eq "wave-$Wave" } | Select-Object -First 1
    if ($label) { return $label.description }
    return "Wave $Wave"
}

$byId = @{}
foreach ($t in $spec.tickets) { $byId[$t.id] = $t }

# Reverse dependencies, so each ticket states what it unblocks.
$blocks = @{}
foreach ($t in $spec.tickets) { $blocks[$t.id] = @() }
foreach ($t in $spec.tickets) {
    foreach ($d in $t.depends) {
        if ($blocks.ContainsKey($d)) { $blocks[$d] += $t.id }
    }
}

# Dependency depth, used to present the parallel batches in the index.
$level = @{}
function Resolve-Level {
    param([string]$Id)
    if ($level.ContainsKey($Id)) { return $level[$Id] }
    $t = $byId[$Id]
    if (-not $t.depends -or $t.depends.Count -eq 0) {
        $level[$Id] = 0
    }
    else {
        $max = 0
        foreach ($d in $t.depends) {
            $l = Resolve-Level -Id $d
            if ($l -gt $max) { $max = $l }
        }
        $level[$Id] = $max + 1
    }
    return $level[$Id]
}
foreach ($t in $spec.tickets) { Resolve-Level -Id $t.id | Out-Null }

$fileNames = @{}
foreach ($t in $spec.tickets) {
    $fileNames[$t.id] = "$($t.id)-$(Get-Slug -Text $t.title).md"
}

# The full verification sequence: a clean build first, the ticket's own commands, then the whole
# suite to catch regressions. Some tickets already name the build or the full suite in their
# verify list, so dedupe while preserving order rather than emitting a command twice.
function Get-VerifyCommands {
    param($Ticket)
    $all = @('dotnet build CouponService.slnx') + @($Ticket.verify) + @('dotnet test CouponService.slnx')
    $seen = @{}
    $result = @()
    foreach ($c in $all) {
        $key = $c.Trim()
        if ($seen.ContainsKey($key)) { continue }
        $seen[$key] = $true
        $result += $key
    }
    return $result
}

# --- The prompt ---------------------------------------------------------------

# Deliberately compact. It names the ticket's own specification file rather than restating the
# goal, criteria and notes, because that file is committed and the agent can read it. Restating
# them here would create two copies in one document, free to drift.
function New-TicketPrompt {
    param($Ticket, [string]$FileName)

    $P = New-Object System.Collections.Generic.List[string]
    $id = $Ticket.id
    $branchSlug = Get-Slug -Text $Ticket.title -MaxLength 40

    $P.Add("Implement ticket $id in this repository, end to end.")
    $P.Add('')
    $P.Add('Read these first, in order. They are the contract and they override anything you assume:')
    $P.Add("  1. .github/tickets/$FileName")
    $P.Add('     This ticket: goal, scope, out of scope, acceptance criteria, implementation notes, verification.')
    $P.Add('  2. AGENTS.md')
    $P.Add('     Standing rules: money as decimal, engine purity, determinism via the injected IClock,')
    $P.Add('     scope discipline, warnings as errors, git conventions, PowerShell 5.1 environment.')
    $P.Add('  3. .kiro/specs/coupon-service/requirements.md')
    $P.Add("     The full text of $($Ticket.acceptance -join ', '). Look each one up and read it. Do not")
    $P.Add('     infer an acceptance criterion from its identifier.')
    $P.Add('  4. .kiro/specs/coupon-service/design.md')
    $P.Add('     Decisions P-1 to P-12 and the exact type signatures. Overrides docs/solution-architecture.md')
    $P.Add('     where the two differ. Consult the architecture document for the reasoning behind a decision.')
    $P.Add('')
    $P.Add('Then:')
    $P.Add("  1. Create branch ticket/$id-$branchSlug from the latest main.")
    $P.Add('  2. Implement the ticket, touching only these paths:')
    foreach ($s in $Ticket.scope) { $P.Add("       $s") }
    $P.Add('     Plus your own new test files, and the checkboxes in .kiro/specs/coupon-service/tasks.md.')
    $P.Add('     Do not reformat or tidy a file you did not otherwise need to change. If you genuinely')
    $P.Add('     need a file outside this list, change it and say so in the pull request.')
    if ($Ticket.packages) {
        $P.Add("  3. You may add these packages, versions centralised in Directory.Packages.props: $($Ticket.packages -join ', ')")
        $P.Add('     Any other package needs a justification in the pull request.')
        $P.Add('  4. Verify. Every command must pass, with zero warnings:')
    }
    else {
        $P.Add('  3. Add no NuGet package. If you believe one is required, stop and say why.')
        $P.Add('  4. Verify. Every command must pass, with zero warnings:')
    }
    foreach ($v in (Get-VerifyCommands -Ticket $Ticket)) { $P.Add("       $v") }
    $P.Add('     Each acceptance criterion needs a test that fails without your change. Prove that by')
    $P.Add('     reverting the change mentally, or temporarily, and confirming the test goes red.')
    $P.Add('  5. Tick the matching checkboxes in .kiro/specs/coupon-service/tasks.md.')
    $P.Add('  6. Commit in atomic, granular steps as you go - one logical change each, every commit')
    $P.Add("     building - with subjects of the form `"$($id): <imperative summary>`". Do not squash the")
    $P.Add('     branch into a single commit; the granularity is what makes the pull request reviewable.')
    $P.Add('     Add no trailer of any kind: no Co-Authored-By, no Signed-off-by, no tool attribution.')
    $P.Add("  7. Push the branch and open a pull request titled `"$($id): $($Ticket.title)`".")
    $P.Add('     In the body, list every acceptance criterion satisfied, anything deliberately deferred,')
    $P.Add('     and any out-of-scope change you had to make.')
    $P.Add('')
    $P.Add('Rules of engagement:')
    $P.Add('  - Do not suppress an analyzer, add NoWarn, or weaken a test to get a green build. Fix the code.')
    $P.Add('  - Do not stub an acceptance criterion and mark it done.')
    $P.Add('  - Never report a result you did not observe.')
    $P.Add('  - Comment only what the code cannot express: an invariant, an external constraint, a')
    $P.Add('    non-obvious trade-off, or the AC or P item that forces a behaviour. Do not narrate the')
    $P.Add('    code, and never explain your change in a comment - that belongs in the commit message.')
    if ($Ticket.labels -contains 'blocked:azure') {
        $P.Add('  - This ticket needs a live Azure subscription, which may not exist yet. Author, commit and')
        $P.Add('    lint the templates; do not claim a deployment happened. Say plainly what remains unverified.')
    }
    $P.Add('  - If you become blocked, stop and report what you tried, what blocked you, and the options you')
    $P.Add('    see. A ticket returned with a clear blocker is a good outcome; one returned green with a')
    $P.Add('    hollowed-out test is not.')
    $P.Add('  - Read the neighbouring code before adding to it, and match its idiom.')
    $P.Add('')
    $P.Add('When you finish, summarise: what you changed, which acceptance criteria are now covered by which')
    $P.Add('tests, the verification output, and anything you deferred or could not do.')

    return $P
}

# --- Ticket body --------------------------------------------------------------

function New-TicketMarkdown {
    param($Ticket)

    $L = New-Object System.Collections.Generic.List[string]
    $id = $Ticket.id
    $file = $fileNames[$id]

    $L.Add("# $($id): $($Ticket.title)")
    $L.Add('')

    # Summary table
    $labelList = (@($Ticket.labels) + @("size:$($Ticket.size)")) -join ', '
    $dependsText = '—'
    if ($Ticket.depends -and $Ticket.depends.Count -gt 0) {
        $parts = @()
        foreach ($d in $Ticket.depends) { $parts += "[$d]($($fileNames[$d]))" }
        $dependsText = $parts -join ', '
    }
    $blocksText = '—'
    if ($blocks[$id].Count -gt 0) {
        $parts = @()
        foreach ($b in ($blocks[$id] | Sort-Object)) { $parts += "[$b]($($fileNames[$b]))" }
        $blocksText = $parts -join ', '
    }

    $L.Add('| | |')
    $L.Add('|---|---|')
    $L.Add("| **Wave** | $($Ticket.wave) — $(Get-WaveName -Spec $spec -Wave $Ticket.wave) |")
    $L.Add("| **Size** | $($Ticket.size) |")
    $L.Add("| **Labels** | ``$labelList`` |")
    $L.Add("| **Blocked by** | $dependsText |")
    $L.Add("| **Blocks** | $blocksText |")
    $L.Add('')

    $L.Add('> **Read [`AGENTS.md`](../../AGENTS.md) before starting.** It carries the standing rules —')
    $L.Add('> money as `decimal`, engine purity, determinism via `IClock`, scope discipline, the definition')
    $L.Add('> of done and the git conventions — which are deliberately not repeated in every ticket.')
    $L.Add('')

    $L.Add('## Goal')
    $L.Add('')
    $L.Add($Ticket.goal)
    $L.Add('')

    if ($Ticket.depends -and $Ticket.depends.Count -gt 0) {
        $L.Add('## Blocked by')
        $L.Add('')
        foreach ($d in $Ticket.depends) {
            $L.Add("- $d — $($byId[$d].title)")
        }
        $L.Add('')
    }

    $L.Add('## Scope — touch only these paths')
    $L.Add('')
    foreach ($s in $Ticket.scope) { $L.Add("- ``$s``") }
    $L.Add('')
    $L.Add('You may additionally add your own new test files, and tick the matching checkboxes in')
    $L.Add('`.kiro/specs/coupon-service/tasks.md`. Any other file you touch must be called out in the')
    $L.Add('pull request under a heading `Out-of-scope changes`.')
    $L.Add('')

    if ($Ticket.outOfScope) {
        $L.Add('## Out of scope')
        $L.Add('')
        foreach ($s in $Ticket.outOfScope) { $L.Add("- $s") }
        $L.Add('')
    }

    $L.Add('## Acceptance criteria')
    $L.Add('')
    $L.Add('Defined in [`requirements.md`](../../.kiro/specs/coupon-service/requirements.md). Look each')
    $L.Add('one up and read the full text; do not infer it from the identifier. This ticket satisfies:')
    $L.Add('')
    foreach ($a in $Ticket.acceptance) { $L.Add("- **$a**") }
    $L.Add('')
    $L.Add('Each one needs a test that would fail without this change.')
    $L.Add('')

    $L.Add('## Implementation notes')
    $L.Add('')
    foreach ($s in $Ticket.steps) { $L.Add("- $s") }
    $L.Add('')

    if ($Ticket.packages) {
        $L.Add('## Packages this ticket may add')
        $L.Add('')
        foreach ($p in $Ticket.packages) { $L.Add("- ``$p``") }
        $L.Add('')
        $L.Add('Versions belong in `Directory.Packages.props`, not in the csproj. Adding a package that is')
        $L.Add('not listed here needs a justification in the pull request.')
        $L.Add('')
    }

    $L.Add('## Verification')
    $L.Add('')
    $L.Add('All of these must pass, with zero warnings. Do not suppress an analyzer to get there.')
    $L.Add('')
    $L.Add('```powershell')
    foreach ($v in (Get-VerifyCommands -Ticket $Ticket)) { $L.Add($v) }
    $L.Add('```')
    $L.Add('')

    $L.Add('## Prompt')
    $L.Add('')
    if ($Ticket.depends -and $Ticket.depends.Count -gt 0) {
        $verb = 'is'
        if ($Ticket.depends.Count -gt 1) { $verb = 'are' }
        $L.Add("Confirm $($Ticket.depends -join ' and ') $verb merged, then paste this into a fresh Cursor")
        $L.Add('chat in this repository. Nothing else is needed: everything is either in the prompt or in a')
        $L.Add('file the prompt names.')
    }
    else {
        $L.Add('Paste this into a fresh Cursor chat in this repository. Nothing else is needed: everything is')
        $L.Add('either in the prompt or in a file the prompt names.')
    }
    $L.Add('')
    $L.Add('```text')
    foreach ($line in (New-TicketPrompt -Ticket $Ticket -FileName $file)) { $L.Add($line) }
    $L.Add('```')
    $L.Add('')

    $L.Add('## Definition of done')
    $L.Add('')
    $L.Add('1. `dotnet build CouponService.slnx` succeeds with zero warnings.')
    $L.Add('2. Every command in Verification passes.')
    $L.Add('3. No previously passing test now fails.')
    $L.Add('4. Every acceptance criterion above is covered by a test that would fail without this change.')
    $L.Add('5. The matching checkboxes in `tasks.md` are ticked.')
    $L.Add('6. The branch is a sequence of atomic commits, none of them carrying a trailer.')
    $L.Add('7. A pull request exists listing the acceptance criteria satisfied and anything deferred.')
    $L.Add('')
    $L.Add('---')
    $L.Add('')
    $L.Add('<sub>Generated from `.github/tickets.json` by `scripts/generate-ticket-docs.ps1`. Edit the JSON, then regenerate.</sub>')

    return ($L -join [Environment]::NewLine) + [Environment]::NewLine
}

# --- Index --------------------------------------------------------------------

function New-IndexMarkdown {
    $L = New-Object System.Collections.Generic.List[string]

    $L.Add('# Ticket index')
    $L.Add('')
    $L.Add("$($spec.tickets.Count) tickets. Standing rules for every one of them live in")
    $L.Add('[`AGENTS.md`](../../AGENTS.md); the acceptance criteria they reference live in')
    $L.Add('[`.kiro/specs/coupon-service/requirements.md`](../../.kiro/specs/coupon-service/requirements.md).')
    $L.Add('')
    $L.Add('Each ticket file ends with a **Prompt** section. Open the ticket, copy that block, paste it into')
    $L.Add('a fresh Cursor chat in this repository. There is nothing to install and no wrapper to run.')
    $L.Add('')
    $L.Add('| Ticket | Title | Wave | Size | Blocked by |')
    $L.Add('|---|---|---|---|---|')
    foreach ($t in ($spec.tickets | Sort-Object { $_.id })) {
        $dep = '—'
        if ($t.depends -and $t.depends.Count -gt 0) { $dep = ($t.depends -join ', ') }
        $L.Add("| [$($t.id)]($($fileNames[$t.id])) | $($t.title) | $($t.wave) | $($t.size) | $dep |")
    }
    $L.Add('')

    $L.Add('## Execution order')
    $L.Add('')
    $L.Add('Tickets in the same batch have no dependency on each other, so their prompts can be running')
    $L.Add('at the same time in separate chats. A batch cannot start until the batch above it is merged.')
    $L.Add('')
    $L.Add('| Batch | Tickets | Concurrency |')
    $L.Add('|---|---|---|')

    $maxLevel = ($level.Values | Measure-Object -Maximum).Maximum
    for ($i = 0; $i -le $maxLevel; $i++) {
        $ids = @($level.Keys | Where-Object { $level[$_] -eq $i } | Sort-Object)
        if ($ids.Count -eq 0) { continue }
        $note = 'one chat'
        if ($ids.Count -gt 1) { $note = "$($ids.Count) chats in parallel" }
        $L.Add("| $($i + 1) | $($ids -join ', ') | $note |")
    }
    $L.Add('')
    $L.Add('Running a batch in parallel means separate chats, not one chat given several prompts. Two')
    $L.Add('agents editing the same working tree will collide; give each its own worktree or branch.')
    $L.Add('')

    $L.Add('## CI and CD')
    $L.Add('')
    $L.Add('**Azure Pipelines only.** There is no GitHub Actions workflow in this delivery. PR builds and')
    $L.Add('the eight-stage deploy path both live in `azure-pipelines.yml` (CS-26), run on Azure DevOps.')
    $L.Add('')

    $late = @($spec.tickets | Where-Object { $_.wave -ge 7 } | Sort-Object { $_.id })
    if ($late.Count -gt 0) {
        $L.Add('## Remaining path (Wave 7+)')
        $L.Add('')
        $L.Add('After CS-01 through CS-24 are merged, work this sequence. CS-25 and CS-26 can start in')
        $L.Add('parallel once their dependencies are met; CS-27 needs CS-25 merged; CS-28 needs CS-27;')
        $L.Add('CS-29 needs CS-26 and CS-28; CS-30 needs CS-29.')
        $L.Add('')
        $L.Add('| Ticket | Title | Blocked by |')
        $L.Add('|---|---|---|')
        foreach ($t in $late) {
            $dep = '—'
            if ($t.depends -and $t.depends.Count -gt 0) { $dep = ($t.depends -join ', ') }
            $L.Add("| [$($t.id)]($($fileNames[$t.id])) | $($t.title) | $dep |")
        }
        $L.Add('')
    }

    $L.Add('## Blocked on Azure')
    $L.Add('')
    $azure = @($spec.tickets | Where-Object { $_.labels -contains 'blocked:azure' } | ForEach-Object { $_.id })
    if ($azure.Count -gt 0) {
        $L.Add("$($azure -join ', ') are authored and linted but cannot be deployed until a subscription")
        $L.Add('exists. Their specifications say so explicitly, and instruct the agent never to report a')
        $L.Add('deployment that did not happen.')
    }
    else {
        $L.Add('None. Live provision and CD proof are CS-27 through CS-29; they assume a subscription and')
        $L.Add('`rg-coupon-demo` already exist.')
    }
    $L.Add('')

    $L.Add('## Regenerating')
    $L.Add('')
    $L.Add('```powershell')
    $L.Add('./scripts/generate-ticket-docs.ps1          # rewrite these files from tickets.json')
    $L.Add('./scripts/generate-ticket-docs.ps1 -Check   # fail if they are stale, for CI')
    $L.Add('```')
    $L.Add('')
    $L.Add('---')
    $L.Add('')
    $L.Add('<sub>Generated from `.github/tickets.json` by `scripts/generate-ticket-docs.ps1`.</sub>')

    return ($L -join [Environment]::NewLine) + [Environment]::NewLine
}

# --- Write or check -----------------------------------------------------------

$written = 0
$stale = @()

function Save-File {
    param([string]$Path, [string]$Content)
    $existing = $null
    if (Test-Path $Path) { $existing = Get-Content $Path -Raw }

    if ($existing -eq $Content) { return $false }

    if ($Check) {
        $script:stale += (Split-Path -Leaf $Path)
        return $false
    }

    Set-Content -Path $Path -Value $Content -Encoding UTF8 -NoNewline
    return $true
}

foreach ($ticket in $spec.tickets) {
    $path = Join-Path $outDir $fileNames[$ticket.id]
    if (Save-File -Path $path -Content (New-TicketMarkdown -Ticket $ticket)) {
        $written++
        Write-Host "  wrote $($fileNames[$ticket.id])" -ForegroundColor DarkGray
    }
}

$indexPath = Join-Path $outDir 'INDEX.md'
if (Save-File -Path $indexPath -Content (New-IndexMarkdown)) {
    $written++
    Write-Host '  wrote INDEX.md' -ForegroundColor DarkGray
}

# Ticket id to file name, so gh-bootstrap.ps1 can locate a ticket's specification without
# re-implementing the slug rules.
$manifest = [ordered]@{}
foreach ($t in ($spec.tickets | Sort-Object { $_.id })) { $manifest[$t.id] = $fileNames[$t.id] }
$manifestJson = (ConvertTo-Json -InputObject $manifest) + [Environment]::NewLine
if (Save-File -Path (Join-Path $outDir 'manifest.json') -Content $manifestJson) {
    $written++
    Write-Host '  wrote manifest.json' -ForegroundColor DarkGray
}

# Remove ticket files that no longer correspond to a ticket, so a renamed ticket
# does not leave a stale duplicate behind.
$expected = @($fileNames.Values) + @('INDEX.md')
foreach ($existing in (Get-ChildItem $outDir -Filter *.md -ErrorAction SilentlyContinue)) {
    if ($expected -notcontains $existing.Name) {
        if ($Check) { $stale += "$($existing.Name) (orphaned)" }
        else {
            Remove-Item $existing.FullName
            Write-Host "  removed orphaned $($existing.Name)" -ForegroundColor DarkYellow
        }
    }
}

Write-Host ''
if ($Check) {
    if ($stale.Count -gt 0) {
        Write-Host "  Ticket docs are stale: $($stale -join ', ')" -ForegroundColor Red
        Write-Host '  Run: ./scripts/generate-ticket-docs.ps1' -ForegroundColor Red
        exit 1
    }
    Write-Host "  Ticket docs are up to date ($($spec.tickets.Count) tickets)." -ForegroundColor Green
    exit 0
}

Write-Host "  $($spec.tickets.Count) tickets, $written file(s) updated in .github/tickets/" -ForegroundColor Green
Write-Host '  Index: .github/tickets/INDEX.md' -ForegroundColor Green
Write-Host ''
exit 0
