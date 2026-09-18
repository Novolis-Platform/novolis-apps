#Requires -Version 7.0
<#
.SYNOPSIS
  Build and install ReadAloud.Android onto a connected USB device.
#>
param(
    [string]$Serial = '',
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

if ($Serial) {
    & $adb -s $Serial get-state
    if ($LASTEXITCODE -ne 0) { throw "adb device $Serial is not ready." }
    $env:ANDROID_SERIAL = $Serial
} else {
    $state = & $adb get-state 2>&1
    if ($state -ne 'device') {
        throw "adb get-state returned '$state' (expected 'device'). Authorize USB debugging and retry."
    }
}

$project = Join-Path $repoRoot 'src/ReadAloud/ReadAloud.Android/ReadAloud.Android.csproj'
Write-Host "Installing $project ($Configuration)…"
dotnet build $project -f net10.0-android -c $Configuration -t:Install
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build -t:Install failed with exit $LASTEXITCODE"
}

Write-Host "Installed. Launch Read Aloud on the phone."
