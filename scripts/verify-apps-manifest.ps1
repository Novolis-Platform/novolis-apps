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
$generated = @(
    Join-Path $RepoRoot 'Novolis.Apps.slnx'
    Get-ChildItem -Path $RepoRoot -Recurse -Filter '*.slnx' -File |
        ForEach-Object { $_.FullName }
)
$relative = $generated |
    Sort-Object -Unique |
    ForEach-Object {
        [IO.Path]::GetRelativePath($RepoRoot, $_).Replace('\', '/')
    }
$dirty = @(git -C $RepoRoot status --short -- $relative)
if ($dirty.Count -gt 0) {
    $dirty | Write-Error
    throw 'Generated solution drift detected. Commit the generated .slnx files.'
}

Write-Host 'Apps manifest + generated solutions OK.'
