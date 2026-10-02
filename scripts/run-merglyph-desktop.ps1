#Requires -Version 7.0
<#
.SYNOPSIS
  Runs the Merglyph Windows MAUI host.
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$project = Join-Path (Split-Path $PSScriptRoot -Parent) 'src/Merglyph/Merglyph/Merglyph.csproj'
dotnet run --project $project `
    -c $Configuration `
    -f net10.0-windows10.0.19041.0 `
    -p:NovolisUseProjectReferences=true `
    -p:NovolisMauiTargetFrameworks=net10.0-windows10.0.19041.0
if ($LASTEXITCODE -ne 0) {
    throw "Merglyph Windows host failed with exit $LASTEXITCODE."
}
