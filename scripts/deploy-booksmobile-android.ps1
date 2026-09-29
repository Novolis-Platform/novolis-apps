#Requires -Version 7.0
<#
.SYNOPSIS
  Build and install BooksMobile.Android onto a connected USB device.
#>
param(
    [string]$Serial = '',
    [string]$ClientId = $env:BOOKSMOBILE_GITHUB_CLIENT_ID,
    [string]$AndroidTool = 'novolis-android',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location $repoRoot

. (Join-Path $PSScriptRoot 'AndroidDeployment.Common.ps1')
$adb = Initialize-NovolisAndroidDeployment -Serial $Serial -AndroidTool $AndroidTool

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

$launchArgs = @()
if ($Serial) { $launchArgs = @('--serial', $Serial) }
& $AndroidTool app launch com.novolis.booksmobile @launchArgs
if ($LASTEXITCODE -ne 0) { throw "Books Mobile installed but could not launch." }

Write-Host "Installed and launched Books Mobile on the phone."
