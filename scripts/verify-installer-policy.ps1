#Requires -Version 7.0
# Static installer citizenship checks against generated Inno script contract + apps.json.
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Publish-NovolisApp.ps1')

$manifest = Get-NovolisAppsManifest -RepoRoot $RepoRoot
$errors = [System.Collections.Generic.List[string]]::new()

# Contract: Packaging.Inno generator must emit required citizenship lines.
$generator = Join-Path (Split-Path $RepoRoot -Parent) 'novolis-avalonia\src\Novolis.Avalonia.Packaging.Inno\InnoScriptGenerator.cs'
if (Test-Path -LiteralPath $generator) {
    $src = Get-Content -LiteralPath $generator -Raw
    foreach ($needle in @(
        'PrivilegesRequired=lowest',
        'UsePreviousAppDir=yes',
        'AllowDowngrade=no',
        'CloseApplications=yes',
        '{localappdata}}\\Programs\\',
        'Flags: unchecked'
    )) {
        if ($src -notlike "*$needle*") {
            $errors.Add("InnoScriptGenerator missing required fragment: $needle")
        }
    }
}
else {
    Write-Warning "Sibling InnoScriptGenerator not found at $generator — skipping source contract check."
}

$appIds = @{}
foreach ($app in $manifest.apps) {
    if (-not ($app.ship -contains 'windows-inno')) { continue }
    if (-not $app.windows) {
        $errors.Add("$($app.key): windows-inno without windows metadata")
        continue
    }
    if ($app.windows.installDir -notlike 'Novolis\*') {
        $errors.Add("$($app.key): installDir must be under Novolis\...")
    }
    if ([string]::IsNullOrWhiteSpace($app.windows.appId)) {
        $errors.Add("$($app.key): missing AppId")
    }
    elseif ($appIds.ContainsKey($app.windows.appId)) {
        $errors.Add("Duplicate AppId $($app.windows.appId)")
    }
    else {
        $appIds[$app.windows.appId] = $app.key
    }
    if ([string]::IsNullOrWhiteSpace($app.windows.closeApplicationsFilter)) {
        $errors.Add("$($app.key): closeApplicationsFilter required")
    }
    if (-not $app.data -or $app.data.retention -ne 'uninstall-preserves-user-data') {
        $errors.Add("$($app.key): data.retention must be uninstall-preserves-user-data")
    }
    if ($app.data.appDataRoot -notlike '*\Novolis\*') {
        $errors.Add("$($app.key): appDataRoot must live under Novolis")
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "Installer policy OK for $($appIds.Count) windows-inno apps."
