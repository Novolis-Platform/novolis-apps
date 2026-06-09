#Requires -Version 7.0
# Publish Manuscript Studio win-x64, optional Inno Setup installer (local parity with CI).
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')),
    [int]$BuildNumber = 0,
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'

$appProject = Join-Path $RepoRoot 'src/ManuscriptStudio/ManuscriptStudio.csproj'
$versionFile = Join-Path $RepoRoot 'build/version.json'
$stagingDir = Join-Path $RepoRoot 'artifacts/manuscript-studio'
$publishDir = Join-Path $stagingDir 'app'
$installerDir = Join-Path $stagingDir 'installer'

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

New-Item -ItemType Directory -Force -Path $publishDir, $installerDir | Out-Null

$versionArgs = @(
    "-p:PackageVersion=$packageVersion"
    "-p:AssemblyVersion=$assemblyVersion"
    "-p:FileVersion=$fileVersion"
    "-p:InformationalVersion=$packageVersion"
)

$cfgArgs = @()
$nugetConfig = Join-Path $RepoRoot 'nuget.config'
if (Test-Path $nugetConfig) {
    $cfgArgs = @('--configfile', $nugetConfig)
}

Write-Host "Publishing Manuscript Studio $packageVersion (win-x64)..."
dotnet restore $appProject -r win-x64 @cfgArgs @versionArgs
if ($LASTEXITCODE -ne 0) { throw "Restore failed with exit code $LASTEXITCODE." }

dotnet publish $appProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    --no-restore `
    -o $publishDir `
    @versionArgs
if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE." }

$zipName = "ManuscriptStudio-$packageVersion-win-x64.zip"
$zipPath = Join-Path $stagingDir $zipName
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath
Write-Host "Portable zip: $zipPath"

if ($SkipInstaller) {
    return
}

$scriptPath = Join-Path $installerDir 'manuscript-studio.iss'
dotnet msbuild $appProject `
    -t:NovolisGenerateInnoScript `
    -p:NovolisInnoAppName='Manuscript Studio' `
    -p:NovolisInnoAppVersion=$packageVersion `
    -p:NovolisInnoPublishDir=$publishDir `
    -p:NovolisInnoAppExeName='ManuscriptStudio.exe' `
    -p:NovolisInnoOutputDir=$installerDir `
    -p:NovolisInnoAppId='Novolis.ManuscriptStudio' `
    -p:NovolisInnoDefaultGroupName='Manuscript Studio' `
    -p:NovolisInnoOutputBaseFilename="ManuscriptStudioSetup-$packageVersion-win-x64" `
    -p:NovolisInnoInstallDirName='Novolis\Manuscript Studio' `
    -p:NovolisInnoScriptPath=$scriptPath

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) {
    $iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1)
}
if (-not $iscc) {
    Write-Warning "ISCC.exe not found. Inno script written to $scriptPath — install Inno Setup 6 to compile the installer."
    return
}

& $iscc $scriptPath
$installer = Join-Path $installerDir "ManuscriptStudioSetup-$packageVersion-win-x64.exe"
if (-not (Test-Path $installer)) {
    throw "Expected installer not found: $installer"
}
Write-Host "Installer: $installer"
