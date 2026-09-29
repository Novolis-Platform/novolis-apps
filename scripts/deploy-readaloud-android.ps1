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

. (Join-Path $PSScriptRoot 'AndroidDeployment.Common.ps1')
$adb = Initialize-NovolisAndroidDeployment -Serial $Serial -AndroidTool $AndroidTool

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
