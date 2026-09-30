#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)
$ErrorActionPreference = 'Stop'
$cs = Join-Path $PSScriptRoot 'verify-installer-policy.cs'
& dotnet run --file $cs -- $RepoRoot
exit $LASTEXITCODE
