#Requires -Version 7.0
<#
.SYNOPSIS
  Build and install Novolis PDF Reader onto a connected Android device or emulator.
#>
param(
    [string]$Serial = '',
    [string]$AndroidTool = 'novolis-android',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location $repoRoot

if ([string]::IsNullOrWhiteSpace($env:ANDROID_HOME)) {
    $defaultSdk = Join-Path $env:LOCALAPPDATA 'Android\Sdk'
    if (Test-Path $defaultSdk) { $env:ANDROID_HOME = $defaultSdk }
}
$adb = Join-Path $env:ANDROID_HOME 'platform-tools\adb.exe'
if (-not (Test-Path $adb)) { throw "adb not found at $adb" }
if (-not [string]::IsNullOrWhiteSpace($Serial)) { $env:ANDROID_SERIAL = $Serial }

$androidTool = Get-Command $AndroidTool -ErrorAction SilentlyContinue
if ($androidTool) {
    . (Join-Path $PSScriptRoot 'AndroidDeployment.Common.ps1')
    $null = Initialize-NovolisAndroidDeployment -Serial $Serial -AndroidTool $AndroidTool
}

$project = Join-Path $repoRoot 'src/NovolisPdfReader/NovolisPdfReader.csproj'
$isEmulator = $Serial.StartsWith('emulator-', [System.StringComparison]::OrdinalIgnoreCase)
Write-Host "Installing $project ($Configuration)$(if ($isEmulator) { ' android-x64' })…"
$extra = @()
if (-not [string]::IsNullOrWhiteSpace($Serial)) {
    $extra += "-p:Device=$Serial"
    $extra += "-p:AdbTarget=-s $Serial"
}
if ($isEmulator) {
    $extra += '-p:RuntimeIdentifier=android-x64'
    $extra += '-p:EmbedAssembliesIntoApk=true'
}
dotnet build $project `
    -f net10.0-android `
    -c $Configuration `
    -t:Install `
    -p:NovolisUseProjectReferences=true `
    -p:NovolisMauiTargetFrameworks=net10.0-android `
    @extra
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build -t:Install failed with exit $LASTEXITCODE"
}

if ($androidTool) {
    $launchArgs = @()
    if ($Serial) { $launchArgs = @('--serial', $Serial) }
    & $AndroidTool app launch com.novolis.pdfreader @launchArgs
    if ($LASTEXITCODE -ne 0) { throw "Novolis PDF Reader installed but could not launch." }
} else {
    $launchSerial = @()
    if ($Serial) { $launchSerial = @('-s', $Serial) }
    & $adb @launchSerial shell monkey -p com.novolis.pdfreader -c android.intent.category.LAUNCHER 1
    if ($LASTEXITCODE -ne 0) { throw "Novolis PDF Reader installed but could not launch." }
}

Write-Host "Installed and launched Novolis PDF Reader on the Android device."
