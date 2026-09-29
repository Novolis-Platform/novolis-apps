#Requires -Version 7.0
<#
.SYNOPSIS
  Installs the developer-only Read Aloud Azure fallback into app-private storage.
#>
param(
    [string]$Serial = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$source = Join-Path $repoRoot 'local\ReadAloud\azure-speech-fallback.json'
$packageName = 'com.novolis.readaloud'
$remoteTemp = '/data/local/tmp/readaloud-azure-fallback.json'

if (-not (Test-Path -LiteralPath $source)) {
    throw "Fallback file not found: $source"
}

if ([string]::IsNullOrWhiteSpace($env:ANDROID_HOME)) {
    $defaultSdk = Join-Path $env:LOCALAPPDATA 'Android\Sdk'
    if (Test-Path -LiteralPath $defaultSdk) {
        $env:ANDROID_HOME = $defaultSdk
    } else {
        throw "ANDROID_HOME is not set and $defaultSdk was not found."
    }
}

$adb = Join-Path $env:ANDROID_HOME 'platform-tools\adb.exe'
if (-not (Test-Path -LiteralPath $adb)) {
    throw "adb not found at $adb"
}

$adbArgs = @()
if (-not [string]::IsNullOrWhiteSpace($Serial)) {
    $adbArgs += @('-s', $Serial)
}

& $adb @adbArgs get-state | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'The selected Android device is not ready.'
}

try {
    & $adb @adbArgs push $source $remoteTemp | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not transfer the fallback file to the Android device.'
    }

    & $adb @adbArgs shell run-as $packageName mkdir -p files
    if ($LASTEXITCODE -ne 0) {
        throw 'The installed Read Aloud package does not allow app-private setup.'
    }

    & $adb @adbArgs shell run-as $packageName cp $remoteTemp files/azure-speech-fallback.json
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not place the fallback file in Read Aloud app storage.'
    }
}
finally {
    & $adb @adbArgs shell rm $remoteTemp | Out-Null
}

Write-Host "Installed the local Read Aloud fallback into $packageName app storage."
