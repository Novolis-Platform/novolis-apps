#Requires -Version 7.0
<#
.SYNOPSIS
  Run BooksMobile on Windows desktop.

.PARAMETER LocalReview
  Clone/update frankhaugen/galactic-confederation-review and open it as a local workspace (no GitHub sign-in).

.PARAMETER LocalWorkspace
  Absolute path to an existing markdown workspace (Manuscript content/ or MkDocs docs/).

.PARAMETER ClientId
  Optional GitHub OAuth client id override for remote sign-in mode.
#>
param(
    [switch]$LocalReview,
    [string]$LocalWorkspace,
    [string]$ClientId = $env:BOOKSMOBILE_GITHUB_CLIENT_ID
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location $repoRoot

if ($LocalReview -and -not [string]::IsNullOrWhiteSpace($LocalWorkspace)) {
    throw "Use either -LocalReview or -LocalWorkspace, not both."
}

if ($LocalReview) {
    $checkout = Join-Path (Split-Path $repoRoot -Parent) 'artifacts\galactic-confederation-review'
    if (-not (Test-Path (Join-Path $checkout '.git'))) {
        New-Item -ItemType Directory -Force -Path (Split-Path $checkout -Parent) | Out-Null
        gh repo clone frankhaugen/galactic-confederation-review $checkout
    } else {
        git -C $checkout pull --ff-only
    }
    $LocalWorkspace = $checkout
}

if (-not [string]::IsNullOrWhiteSpace($LocalWorkspace)) {
    if (-not (Test-Path $LocalWorkspace)) {
        throw "Local workspace not found: $LocalWorkspace"
    }
    $env:BOOKSMOBILE_LOCAL_WORKSPACE = (Resolve-Path $LocalWorkspace).Path
    $env:BOOKSMOBILE_REPO_NAME = 'galactic-confederation-review'
    $env:BOOKSMOBILE_CONTENT_PREFIX = 'docs/'
    Write-Host "Local workspace: $($env:BOOKSMOBILE_LOCAL_WORKSPACE)"
} else {
    Remove-Item Env:BOOKSMOBILE_LOCAL_WORKSPACE -ErrorAction SilentlyContinue
    if ([string]::IsNullOrWhiteSpace($ClientId)) {
        Write-Warning "BOOKSMOBILE_GITHUB_CLIENT_ID is empty. Sign-in will use the baked-in client id."
    } else {
        $env:BOOKSMOBILE_GITHUB_CLIENT_ID = $ClientId
    }
}

$project = Join-Path $repoRoot 'src/BooksMobile/BooksMobile.Desktop/BooksMobile.Desktop.csproj'
dotnet run --project $project -c Debug
