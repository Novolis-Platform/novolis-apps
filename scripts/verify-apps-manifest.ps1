#Requires -Version 7.0
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'
$tool = Join-Path $RepoRoot 'tools/AppsManifest/AppsManifest.csproj'
& dotnet run --project $tool --no-launch-profile -- validate --repo $RepoRoot
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& dotnet run --project $tool --no-launch-profile -- generate-solutions --repo $RepoRoot
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Ensure generated solutions are committed (no dirty drift when run in CI after generate).
Write-Host 'Apps manifest + solutions OK.'
