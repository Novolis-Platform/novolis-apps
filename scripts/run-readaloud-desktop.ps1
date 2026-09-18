#Requires -Version 7.0
<#
.SYNOPSIS
  Run Read Aloud on Windows desktop.
#>
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repoRoot 'src/ReadAloud/ReadAloud.Desktop/ReadAloud.Desktop.csproj'
Write-Host "Running $project…"
dotnet run --project $project
if ($LASTEXITCODE -ne 0) {
    throw "dotnet run failed with exit $LASTEXITCODE"
}
