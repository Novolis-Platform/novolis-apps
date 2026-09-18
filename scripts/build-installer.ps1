#Requires -Version 7.0
# Publish novolis-apps windows-inno products from build/apps.json.
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$App = 'All',
    [int]$BuildNumber = 0,
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Publish-NovolisApp.ps1')

$versionFile = Join-Path $RepoRoot 'build/version.json'
if (-not (Test-Path $versionFile)) {
    throw "Missing $versionFile"
}

$v = Get-Content $versionFile -Raw | ConvertFrom-Json
$year = [int]($v.year ?? $v.sdkYear)
$major = [int]($v.major ?? $v.apiBreak)
$minor = [int]($v.minor ?? $v.feature)
$platform = "$year.$major.$minor"
if ($BuildNumber -le 0) {
    $BuildNumber = 1
}
$packageVersion = "$platform.$BuildNumber"
$assemblyVersion = "$year.$major.0.0"
$fileVersion = $packageVersion

$catalog = @(Get-NovolisAppCatalog -RepoRoot $RepoRoot)
$selected = if ($App -eq 'All') {
    $catalog
}
else {
    @($catalog | Where-Object { $_.Choice -eq $App -or $_.Key -eq $App })
}
if ($selected.Count -eq 0) {
    $choices = ($catalog | ForEach-Object { $_.Choice }) -join ', '
    throw "Unknown app selection: $App. Known windows-inno apps: $choices"
}

$published = [System.Collections.Generic.List[object]]::new()
foreach ($entry in $selected) {
    $item = Publish-NovolisApp `
        -RepoRoot $RepoRoot `
        -AppKey $entry.Key `
        -ProjectRelativePath $entry.Project `
        -PackageVersion $packageVersion `
        -AssemblyVersion $assemblyVersion `
        -FileVersion $fileVersion `
        -SkipInstaller:$SkipInstaller |
        Where-Object { $_.ZipPath } |
        Select-Object -Last 1
    if (-not $item -or -not $item.ZipPath) {
        throw "Publish-NovolisApp did not return a ZipPath for $($entry.Key)."
    }
    $published.Add($item)
}

if ($SkipInstaller) {
    return
}

$lines = @()
foreach ($item in $published) {
    if (Test-Path -LiteralPath $item.ZipPath) {
        $hashZip = (Get-FileHash -LiteralPath $item.ZipPath -Algorithm SHA256).Hash
        $lines += "$hashZip  $($item.ZipName)"
    }
    if ($item.InstallerPath -and (Test-Path -LiteralPath $item.InstallerPath)) {
        $hashExe = (Get-FileHash -LiteralPath $item.InstallerPath -Algorithm SHA256).Hash
        $lines += "$hashExe  $($item.InstallerName)"
    }
}

if ($lines.Count -gt 0) {
    $sumsPath = Join-Path $RepoRoot 'artifacts/SHA256SUMS.txt'
    $lines | Set-Content -Path $sumsPath -Encoding utf8NoBOM
    Write-Host "Checksums: $sumsPath"
}
