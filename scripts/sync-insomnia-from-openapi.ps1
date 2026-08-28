# Sync Insomnia workspace from build-time OpenAPI documents.
# Called automatically after API project builds; safe to run manually:
#   ./scripts/sync-insomnia-from-openapi.ps1
param(
    [string] $RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

$toolProject = Join-Path $RepoRoot 'tools/OpenApiInsomniaSync/OpenApiInsomniaSync.csproj'
if (-not (Test-Path $toolProject)) {
    throw "OpenApiInsomniaSync tool not found: $toolProject"
}

dotnet run --project $toolProject -- $RepoRoot
if ($LASTEXITCODE -ne 0) {
    throw "Insomnia sync failed with exit code $LASTEXITCODE"
}
