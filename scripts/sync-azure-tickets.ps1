<#
.SYNOPSIS
    Creates or refreshes only the Azure-path GitHub issues: CS-25 through CS-30.

.DESCRIPTION
    CS-01 through CS-24 are left untouched. CS-25 and CS-26 (already mapped) get their
    title, body and labels refreshed from the generated ticket markdown. CS-27 through
    CS-30 are created if missing and added to .github/ticket-map.json.

.EXAMPLE
    ./scripts/sync-azure-tickets.ps1 -DryRun
    ./scripts/sync-azure-tickets.ps1
    ./scripts/sync-azure-tickets.ps1 -Owner r4rad -Repo coupon-service
#>
[CmdletBinding()]
param(
    [string] $Owner,
    [string] $Repo = 'coupon-service',
    [switch] $SkipProject,
    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'
$bootstrap = Join-Path $PSScriptRoot 'gh-bootstrap.ps1'
if (-not (Test-Path $bootstrap)) { throw "Not found: $bootstrap" }

# Only the reshaped / new Azure-path tickets. Do not expand this list casually —
# CS-01..CS-24 issues must not be rewritten by this script.
$ids = @('CS-25', 'CS-26', 'CS-27', 'CS-28', 'CS-29', 'CS-30')

$params = @{
    SkipRepoCreate = $true
    Ids            = $ids
    Repo           = $Repo
}
if ($Owner)       { $params['Owner'] = $Owner }
if ($SkipProject) { $params['SkipProject'] = $true }
if ($DryRun)      { $params['DryRun'] = $true }

Write-Host "Syncing GitHub issues for: $($ids -join ', ')" -ForegroundColor Cyan
Write-Host '  CS-25, CS-26 — refresh existing issues from regenerated markdown' -ForegroundColor DarkGray
Write-Host '  CS-27..CS-30 — create if missing' -ForegroundColor DarkGray
Write-Host ''

& $bootstrap @params
exit $LASTEXITCODE
