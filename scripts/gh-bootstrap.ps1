<#
.SYNOPSIS
    Creates the GitHub repository, labels, issues and project board for the Coupon Service
    from .github/tickets.json.

.DESCRIPTION
    Idempotent where GitHub allows it. Issues are created in dependency order so that each
    ticket can reference its blockers by real issue number.

    Prerequisite, and it needs a browser so it cannot be automated:
        gh auth login

.PARAMETER AzureDevOpsRemote
    Optional. Adds a second remote named 'azure' and pushes to it as well. The assignment
    requires the codebase to live in an Azure DevOps repository, so use this to keep GitHub
    for tickets while Azure DevOps remains the repository of record.

.EXAMPLE
    ./scripts/gh-bootstrap.ps1 -Visibility public -DryRun
    ./scripts/gh-bootstrap.ps1 -Visibility public
    ./scripts/gh-bootstrap.ps1 -AzureDevOpsRemote https://dev.azure.com/org/proj/_git/coupon-service
    ./scripts/gh-bootstrap.ps1 -SkipProject -Ids CS-25,CS-26,CS-27,CS-28,CS-29,CS-30
#>
[CmdletBinding()]
param(
    [string]   $Owner,
    [string]   $Repo = 'coupon-service',
    [ValidateSet('private', 'public')]
    [string]   $Visibility = 'public',
    [string]   $AzureDevOpsRemote,
    [switch]   $SkipRepoCreate,
    [switch]   $SkipProject,
    # Only create or refresh these ticket ids. Existing map entries get title/body/labels
    # updated; missing ids are created. All other issues are left untouched.
    [string[]] $Ids,
    [switch]   $DryRun
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$ticketFile = Join-Path $repoRoot '.github/tickets.json'
$mapFile = Join-Path $repoRoot '.github/ticket-map.json'

# Narrow runs must not recreate the repo or re-push remotes.
if ($Ids -and $Ids.Count -gt 0) {
    $SkipRepoCreate = $true
    if ($AzureDevOpsRemote) {
        Write-Warning '-Ids was set; ignoring -AzureDevOpsRemote for this run.'
        $AzureDevOpsRemote = $null
    }
}

function Write-Step { param([string]$Message) Write-Host "`n=== $Message" -ForegroundColor Cyan }
function Write-Info { param([string]$Message) Write-Host "    $Message" -ForegroundColor DarkGray }

# PowerShell 5.1 surfaces native-command stderr as an ErrorRecord, which terminates when
# ErrorActionPreference is Stop. Every native call goes through here so that a tool writing
# to stderr is treated as data plus an exit code, which is what it actually is.
function Invoke-Native {
    param([string]$Exe, [string[]]$Arguments)
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $raw = & $Exe @Arguments 2>&1
        return [pscustomobject]@{
            Output = (($raw | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine)
            Code   = $LASTEXITCODE
        }
    }
    finally { $ErrorActionPreference = $previous }
}

function Invoke-Gh {
    param([string[]]$Arguments, [switch]$AllowFailure)
    if ($DryRun) {
        Write-Host "    [dry-run] gh $($Arguments -join ' ')" -ForegroundColor Yellow
        return ''
    }
    $result = Invoke-Native -Exe 'gh' -Arguments $Arguments
    if ($result.Code -ne 0 -and -not $AllowFailure) {
        throw "gh $($Arguments -join ' ') failed:`n$($result.Output)"
    }
    return $result.Output
}

# --- Preconditions ------------------------------------------------------------
Write-Step 'Checking prerequisites'

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'GitHub CLI not found. Install it from https://cli.github.com and run: gh auth login'
}

$authCheck = Invoke-Native -Exe 'gh' -Arguments @('auth', 'status')
if ($authCheck.Code -ne 0) {
    if ($DryRun) {
        Write-Warning 'Not authenticated (gh auth login). Continuing because -DryRun was set.'
    }
    else {
        throw 'Not authenticated. Run: gh auth login'
    }
}
else {
    Write-Info 'gh authenticated'
}

if (-not (Test-Path $ticketFile)) { throw "Ticket definitions not found at $ticketFile" }
$spec = Get-Content $ticketFile -Raw | ConvertFrom-Json
Write-Info "$($spec.tickets.Count) tickets loaded"

# Issue bodies are the generated markdown specifications, so an issue and its ticket file
# cannot drift apart. Regenerate them if the manifest is missing or stale.
$manifestPath = Join-Path $repoRoot '.github/tickets/manifest.json'
if (-not (Test-Path $manifestPath)) {
    Write-Info 'ticket specifications not generated yet, generating now'
    & (Join-Path $PSScriptRoot 'generate-ticket-docs.ps1') | Out-Null
}
if (-not (Test-Path $manifestPath)) {
    throw 'Could not generate ticket specifications. Run ./scripts/generate-ticket-docs.ps1 and check the error.'
}
$ticketFiles = @{}
$manifestRaw = Get-Content $manifestPath -Raw | ConvertFrom-Json
foreach ($p in $manifestRaw.PSObject.Properties) { $ticketFiles[$p.Name] = $p.Value }
Write-Info "$($ticketFiles.Count) ticket specifications found"

if (-not $Owner) {
    $Owner = (Invoke-Gh -Arguments @('api', 'user', '--jq', '.login')).Trim()
    if (-not $Owner -and -not $DryRun) { throw 'Could not determine GitHub owner. Pass -Owner.' }
    if (-not $Owner) { $Owner = 'OWNER' }
}
$slug = "$Owner/$Repo"
Write-Info "target repository: $slug ($Visibility)"

# --- Repository ---------------------------------------------------------------
Write-Step 'Repository'

$repoExists = (Invoke-Native -Exe 'gh' -Arguments @('repo', 'view', $slug)).Code -eq 0

if ($repoExists) {
    Write-Info 'already exists, leaving it alone'
}
elseif ($SkipRepoCreate) {
    Write-Info 'does not exist and -SkipRepoCreate was set'
}
else {
    Push-Location $repoRoot
    try {
        $hasCommits = (Invoke-Native -Exe 'git' -Arguments @('log', '--oneline', '-1')).Code -eq 0
        if (-not $hasCommits) {
            Write-Info 'no commits yet, creating the initial commit'
            if (-not $DryRun) {
                Invoke-Native -Exe 'git' -Arguments @('add', '-A') | Out-Null
                Invoke-Native -Exe 'git' -Arguments @('commit', '-m', 'Initial commit: architecture, specs, ticket definitions and project skeleton') | Out-Null
            }
            else { Write-Host '    [dry-run] git add -A; git commit' -ForegroundColor Yellow }
        }
        Invoke-Gh -Arguments @('repo', 'create', $slug, "--$Visibility", '--source', '.', '--remote', 'origin', '--push') | Out-Null
        Write-Info 'created and pushed'
    }
    finally { Pop-Location }
}

if ($AzureDevOpsRemote) {
    Write-Step 'Azure DevOps remote (repository of record for the assignment)'
    Push-Location $repoRoot
    try {
        $remotes = (Invoke-Native -Exe 'git' -Arguments @('remote')).Output
        if ($remotes -notmatch '(?m)^azure$') {
            if ($DryRun) { Write-Host "    [dry-run] git remote add azure $AzureDevOpsRemote" -ForegroundColor Yellow }
            else { Invoke-Native -Exe 'git' -Arguments @('remote', 'add', 'azure', $AzureDevOpsRemote) | Out-Null }
            Write-Info "added remote 'azure' -> $AzureDevOpsRemote"
        }
        if ($DryRun) { Write-Host '    [dry-run] git push azure main' -ForegroundColor Yellow }
        else { Invoke-Native -Exe 'git' -Arguments @('push', 'azure', 'main') | Out-Null }
        Write-Info 'pushed to Azure DevOps'
    }
    finally { Pop-Location }
}

# --- Labels -------------------------------------------------------------------
Write-Step 'Labels'
foreach ($label in $spec.labels) {
    Invoke-Gh -AllowFailure -Arguments @(
        'label', 'create', $label.name,
        '--repo', $slug,
        '--color', $label.color,
        '--description', $label.description,
        '--force'
    ) | Out-Null
    Write-Info $label.name
}

# --- Order tickets so blockers are created first -------------------------------
Write-Step 'Resolving dependency order'

$ordered = @()
$remaining = [System.Collections.ArrayList]::new()
foreach ($t in $spec.tickets) { [void]$remaining.Add($t) }

while ($remaining.Count -gt 0) {
    $placed = @()
    foreach ($t in $remaining) {
        $unmet = @($t.depends | Where-Object { $_ -and ($ordered.id -notcontains $_) })
        if ($unmet.Count -eq 0) { $placed += $t }
    }
    if ($placed.Count -eq 0) {
        throw "Circular or unresolvable dependency among: $(($remaining | ForEach-Object { $_.id }) -join ', ')"
    }
    foreach ($t in $placed) {
        $ordered += $t
        $remaining.Remove($t)
    }
}
Write-Info ((($ordered | ForEach-Object { $_.id }) -join ' -> '))

# Optional: only create or refresh a named subset (e.g. after reshaping Wave 7/8).
$idFilter = $null
if ($Ids -and $Ids.Count -gt 0) {
    $idFilter = @{}
    foreach ($raw in $Ids) {
        foreach ($piece in ($raw -split '[,;\s]+')) {
            if ($piece) { $idFilter[$piece.Trim().ToUpperInvariant()] = $true }
        }
    }
    $unknown = @($idFilter.Keys | Where-Object { $spec.tickets.id -notcontains $_ } | Sort-Object)
    if ($unknown.Count -gt 0) {
        throw "Unknown ticket id(s): $($unknown -join ', '). Check .github/tickets.json."
    }
    $ordered = @($ordered | Where-Object { $idFilter.ContainsKey($_.id) })
    Write-Info "filtering to $($ordered.Count) ticket(s): $(($ordered | ForEach-Object { $_.id }) -join ', ')"
}

# --- Issues -------------------------------------------------------------------
Write-Step 'Issues'

$map = @{}
if (Test-Path $mapFile) {
    $existingMap = Get-Content $mapFile -Raw | ConvertFrom-Json
    foreach ($p in $existingMap.PSObject.Properties) { $map[$p.Name] = $p.Value }
    Write-Info "reusing $($map.Count) previously created issue numbers"
}

function New-IssueBody {
    param($Ticket, $Map, $Files, [string]$RepoSlug)

    if (-not $Files.ContainsKey($Ticket.id)) {
        throw "No generated specification for $($Ticket.id). Run: ./scripts/generate-ticket-docs.ps1"
    }
    $fileName = $Files[$Ticket.id]
    $path = Join-Path $repoRoot ".github/tickets/$fileName"
    if (-not (Test-Path $path)) {
        throw "Missing $path. Run: ./scripts/generate-ticket-docs.ps1"
    }

    $body = Get-Content $path -Raw

    # The markdown names blockers by ticket id. On GitHub an issue number is more useful,
    # because it renders as a live cross-reference with open or closed state.
    foreach ($dep in $Ticket.depends) {
        if ($Map.ContainsKey($dep) -and $Map[$dep] -gt 0) {
            $escaped = [regex]::Escape($dep)
            $body = $body -replace "(?m)^- $escaped ", "- $dep (#$($Map[$dep])) "
        }
    }

    # Relative links work in the repository but not inside an issue body, so absolutise them.
    $blobUrl = "https://github.com/$RepoSlug/blob/main"
    $body = $body -replace '\]\(\.\./\.\./', "]($blobUrl/"
    $body = $body -replace '\]\((CS-\d+-[^)]+\.md)\)', "]($blobUrl/.github/tickets/`$1)"

    $nl = [Environment]::NewLine
    $footer = $nl + '---' + $nl + $nl +
              "<sub>Specification of record: [``.github/tickets/$fileName``]($blobUrl/.github/tickets/$fileName). " +
              'Generated from `.github/tickets.json` — edit the JSON and regenerate rather than editing this issue body.</sub>' + $nl

    return $body + $footer
}

foreach ($ticket in $ordered) {
    $body = New-IssueBody -Ticket $ticket -Map $map -Files $ticketFiles -RepoSlug $slug
    $tmp = [System.IO.Path]::GetTempFileName()
    Set-Content -Path $tmp -Value $body -Encoding UTF8

    $labels = @($ticket.labels) + @("size:$($ticket.size)")
    $title = "$($ticket.id): $($ticket.title)"

    try {
        if ($map.ContainsKey($ticket.id) -and $map[$ticket.id] -gt 0) {
            $number = $map[$ticket.id]
            Write-Info "$($ticket.id) already exists as #$number — refreshing title, body and labels"
            $editArgs = @(
                'issue', 'edit', "$number",
                '--repo', $slug,
                '--title', $title,
                '--body-file', $tmp
            )
            foreach ($l in $labels) { $editArgs += @('--add-label', $l) }
            Invoke-Gh -Arguments $editArgs | Out-Null
            if ($DryRun) { Write-Info "$($ticket.id) (dry-run refresh #$number)" }
            else { Write-Info "$($ticket.id) refreshed #$number" }
            continue
        }

        $ghArgs = @(
            'issue', 'create',
            '--repo', $slug,
            '--title', $title,
            '--body-file', $tmp
        )
        foreach ($l in $labels) { $ghArgs += @('--label', $l) }

        $url = (Invoke-Gh -Arguments $ghArgs).Trim()
        if ($DryRun) {
            $map[$ticket.id] = 0
            Write-Info "$($ticket.id) (dry run create)"
        }
        else {
            $number = ($url -split '/')[-1]
            $map[$ticket.id] = [int]$number
            Write-Info "$($ticket.id) -> #$number"
        }
    }
    finally {
        Remove-Item $tmp -ErrorAction SilentlyContinue
    }
}

if (-not $DryRun) {
    $ordered_map = [ordered]@{}
    foreach ($entry in ($map.GetEnumerator() | Sort-Object Name)) {
        $ordered_map[$entry.Name] = $entry.Value
    }
    ConvertTo-Json -InputObject $ordered_map | Set-Content -Path $mapFile -Encoding UTF8
    Write-Info 'issue map written to .github/ticket-map.json'
}

# --- Project board ------------------------------------------------------------
if (-not $SkipProject) {
    Write-Step 'Project board'
    $projectNumber = $null
    $existing = Invoke-Gh -AllowFailure -Arguments @('project', 'list', '--owner', $Owner, '--format', 'json')
    if ($existing -and -not $DryRun) {
        try {
            $projects = ($existing | ConvertFrom-Json).projects
            $match = $projects | Where-Object { $_.title -eq $spec.project.title } | Select-Object -First 1
            if ($match) { $projectNumber = $match.number; Write-Info "reusing project #$projectNumber" }
        }
        catch { }
    }

    if (-not $projectNumber) {
        $created = Invoke-Gh -AllowFailure -Arguments @('project', 'create', '--owner', $Owner, '--title', $spec.project.title, '--format', 'json')
        if ($created -and -not $DryRun) {
            try { $projectNumber = ($created | ConvertFrom-Json).number; Write-Info "created project #$projectNumber" } catch { }
        }
    }

    if ($projectNumber) {
        foreach ($ticket in $ordered) {
            if (-not $map.ContainsKey($ticket.id)) { continue }
            $issueUrl = "https://github.com/$slug/issues/$($map[$ticket.id])"
            Invoke-Gh -AllowFailure -Arguments @('project', 'item-add', $projectNumber, '--owner', $Owner, '--url', $issueUrl) | Out-Null
        }
        Write-Info 'all issues added to the board'
    }
    else {
        Write-Info 'could not resolve a project number; add issues to a board manually or re-run with a project scope token'
        Write-Info 'if this failed on permissions, run: gh auth refresh -s project'
    }
}

Write-Step 'Done'
Write-Host ''
Write-Host "  Repository : https://github.com/$slug" -ForegroundColor Green
Write-Host "  Issues     : https://github.com/$slug/issues" -ForegroundColor Green
Write-Host ''
if ($idFilter) {
    Write-Host '  Synced:' -ForegroundColor Green
    foreach ($t in $ordered) {
        $n = if ($map.ContainsKey($t.id) -and $map[$t.id] -gt 0) { "#$($map[$t.id])" } else { '(new)' }
        Write-Host "      $($t.id) $n — $($t.title)" -ForegroundColor Green
    }
    Write-Host '  Start next: .github/tickets/CS-25-bicep-modules-for-the-whole-environment.md' -ForegroundColor Green
}
else {
    Write-Host '  To start the first ticket, open its specification, copy the Prompt block, and paste it' -ForegroundColor Green
    Write-Host '  into a fresh Cursor chat in this repository:' -ForegroundColor Green
    Write-Host "      .github/tickets/$($ticketFiles['CS-01'])" -ForegroundColor Green
    Write-Host '  Batch order for the rest: .github/tickets/INDEX.md' -ForegroundColor Green
}
Write-Host ''

exit 0
