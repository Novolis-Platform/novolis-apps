#Requires -Version 7.0
<#
.SYNOPSIS
  Runs the Novolis PDF Reader Windows MAUI host.
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$project = Join-Path (Split-Path $PSScriptRoot -Parent) 'src/NovolisPdfReader/NovolisPdfReader.csproj'
dotnet run --project $project `
    -c $Configuration `
    -f net10.0-windows10.0.19041.0 `
    -p:NovolisUseProjectReferences=true `
    -p:NovolisMauiTargetFrameworks=net10.0-windows10.0.19041.0
if ($LASTEXITCODE -ne 0) {
    throw "Novolis PDF Reader Windows host failed with exit $LASTEXITCODE."
}
