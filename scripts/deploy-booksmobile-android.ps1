#Requires -Version 7.0
<#
.SYNOPSIS
  Build and install BooksMobile.Android onto a connected USB device.
#>
param(
    [string]$Serial = '',
    [string]$ClientId = $env:BOOKSMOBILE_GITHUB_CLIENT_ID,
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

if (-not [string]::IsNullOrWhiteSpace($ClientId)) {
    $env:BOOKSMOBILE_GITHUB_CLIENT_ID = $ClientId
}

$project = Join-Path $repoRoot 'src/BooksMobile/BooksMobile.Android/BooksMobile.Android.csproj'
Write-Host "Installing $project ($Configuration)…"
dotnet build $project -f net10.0-android -c $Configuration -t:Install
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build -t:Install failed with exit $LASTEXITCODE"
}

if (-not [string]::IsNullOrWhiteSpace($ClientId)) {
    $serialArgs = @()
    if ($Serial) { $serialArgs = @('-s', $Serial) }
    Write-Host "Pushing GitHub client id into app private storage…"
    $tmp = Join-Path $env:TEMP 'booksmobile-github-client-id.txt'
    Set-Content -Path $tmp -Value $ClientId.Trim() -NoNewline
    & $adb @serialArgs push $tmp /data/local/tmp/booksmobile-github-client-id.txt | Out-Null
    & $adb @serialArgs shell "run-as com.novolis.booksmobile sh -c 'mkdir -p files/BooksMobile && cp /data/local/tmp/booksmobile-github-client-id.txt files/BooksMobile/github-client-id.txt'"
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Could not push client id via run-as (release builds may block this). Set BOOKSMOBILE_GITHUB_CLIENT_ID for desktop instead."
    }
}

Write-Host "Installed. Launch Books Mobile on the phone."
