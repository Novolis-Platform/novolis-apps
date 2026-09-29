#Requires -Version 7.0
<#
.SYNOPSIS
  Build and install ReadAloud.Android onto a connected USB device.
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
    if (Test-Path $defaultSdk) {
        $env:ANDROID_HOME = $defaultSdk
        Write-Host "ANDROID_HOME=$env:ANDROID_HOME"
    } else {
        throw "ANDROID_HOME is not set and $defaultSdk was not found."
    }
}

$adb = Join-Path $env:ANDROID_HOME 'platform-tools\adb.exe'
if (-not (Test-Path $adb)) {
    throw "adb not found at $adb"
}

$tool = Get-Command $AndroidTool -ErrorAction SilentlyContinue
if ($null -eq $tool) {
    throw "$AndroidTool was not found. Install Novolis.Tools.Android.Cli or pass -AndroidTool with an installed command."
}

if ($Serial) {
    $env:ANDROID_SERIAL = $Serial
}

$toolArgs = @('info')
if ($Serial) { $toolArgs += @('--serial', $Serial) }
& $AndroidTool @toolArgs
if ($LASTEXITCODE -ne 0) { throw "$AndroidTool could not select a ready Android device." }

$project = Join-Path $repoRoot 'src/ReadAloud/ReadAloud.Android/ReadAloud.Android.csproj'
Write-Host "Installing $project ($Configuration)…"
dotnet build $project -f net10.0-android -c $Configuration -t:Install
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build -t:Install failed with exit $LASTEXITCODE"
}

$launchArgs = @()
if ($Serial) { $launchArgs = @('--serial', $Serial) }
& $AndroidTool app launch com.novolis.readaloud @launchArgs
if ($LASTEXITCODE -ne 0) { throw "ReadAloud installed but could not launch." }

Write-Host "Installed and launched ReadAloud on the phone."
