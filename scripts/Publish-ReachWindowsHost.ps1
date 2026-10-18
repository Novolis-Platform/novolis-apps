#Requires -Version 7.0
<#
.SYNOPSIS
  Publishes the Reach Windows host bundle for the Hyper-V guest.

.DESCRIPTION
  The service and interactive host are published into one directory because
  the service starts the interactive host from beside its own executable.
  The result is self-contained and can be copied to the Windows guest without
  installing the .NET runtime there.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$OutputRoot = '',

    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path `
        $repoRoot `
        'artifacts\publish\Reach.WindowsHost.Dev'
}
$outputPath = [IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

$serviceProject = Join-Path `
    $repoRoot `
    'src\Reach\Reach.Host.Windows.Service\Reach.Host.Windows.Service.csproj'
$sessionProject = Join-Path `
    $repoRoot `
    'src\Reach\Reach.Host.Windows\Reach.Host.Windows.csproj'

function Publish-ReachProject {
    param(
        [Parameter(Mandatory)][string]$Project,
        [Parameter(Mandatory)][string]$Output
    )

    $arguments = @(
        'publish',
        $Project,
        '-c', $Configuration,
        '-r', 'win-x64',
        '--self-contained', 'true',
        '-p:NovolisUseProjectReferences=false',
        '-p:PublishSingleFile=false',
        '-p:PublishTrimmed=false',
        '-o', $Output
    )
    if ($NoRestore) {
        $arguments += '--no-restore'
    }

    Write-Host "Publishing $Project"
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $Project with exit code $LASTEXITCODE."
    }
}

Publish-ReachProject -Project $serviceProject -Output $outputPath
Publish-ReachProject -Project $sessionProject -Output $outputPath

$guestInstaller = Join-Path $PSScriptRoot 'Install-ReachWindowsHost.ps1'
Copy-Item -LiteralPath $guestInstaller -Destination $outputPath -Force

$requiredFiles = @(
    'Novolis.Reach.Host.Windows.Service.exe',
    'Novolis.Reach.Host.Windows.exe',
    'Install-ReachWindowsHost.ps1'
)
$missing = $requiredFiles |
    Where-Object { -not (Test-Path -LiteralPath (Join-Path $outputPath $_)) }
if ($missing) {
    throw "The published Reach host bundle is incomplete: $($missing -join ', ')"
}

[pscustomobject]@{
    OutputRoot = $outputPath
    ServiceExecutable = Join-Path $outputPath 'Novolis.Reach.Host.Windows.Service.exe'
    SessionExecutable = Join-Path $outputPath 'Novolis.Reach.Host.Windows.exe'
    GuestInstaller = Join-Path $outputPath 'Install-ReachWindowsHost.ps1'
    Configuration = $Configuration
    Runtime = 'win-x64'
} | Format-List

Write-Host "Copy this directory into the Windows guest, then run Install-ReachWindowsHost.ps1 from an elevated PowerShell there."
