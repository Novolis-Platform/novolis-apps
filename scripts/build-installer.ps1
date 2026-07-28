#Requires -Version 7.0
# Publish novolis-apps WinExe projects (win-x64) with optional Inno Setup installers.
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')),
    [ValidateSet('ManuscriptStudio', 'ConceptStudio', 'All')]
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

$apps = switch ($App) {
    'ManuscriptStudio' { @('manuscript-studio') }
    'ConceptStudio' { @('concept-studio') }
    default { @('manuscript-studio', 'concept-studio') }
}

$projectMap = @{
    'manuscript-studio' = 'src/ManuscriptStudio/ManuscriptStudio.csproj'
    'concept-studio'    = 'src/ConceptStudio/ConceptStudio.csproj'
}

$published = [System.Collections.Generic.List[object]]::new()
foreach ($appKey in $apps) {
    $item = Publish-NovolisApp `
        -RepoRoot $RepoRoot `
        -AppKey $appKey `
        -ProjectRelativePath $projectMap[$appKey] `
        -PackageVersion $packageVersion `
        -AssemblyVersion $assemblyVersion `
        -FileVersion $fileVersion `
        -SkipInstaller:$SkipInstaller |
        Where-Object { $_.ZipPath } |
        Select-Object -Last 1
    if (-not $item -or -not $item.ZipPath) {
        throw "Publish-NovolisApp did not return a ZipPath for $appKey."
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
