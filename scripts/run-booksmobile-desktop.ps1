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

.PARAMETER Device
  Desktop geometry: desktop (default), flip8 (540x1260), flip8-native (1080x2520).

.PARAMETER BooksWorkspace
  When set with -LocalWorkspace omitted, open this NMP books checkout (repo books, src/).
#>
param(
    [switch]$LocalReview,
    [string]$LocalWorkspace,
    [string]$BooksWorkspace,
    [ValidateSet('desktop', 'flip8', 'flip8-native')]
    [string]$Device = 'desktop',
    [string]$ClientId = $env:BOOKSMOBILE_GITHUB_CLIENT_ID
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location $repoRoot

if ($LocalReview -and (-not [string]::IsNullOrWhiteSpace($LocalWorkspace) -or -not [string]::IsNullOrWhiteSpace($BooksWorkspace))) {
    throw "Use only one of -LocalReview, -LocalWorkspace, or -BooksWorkspace."
}
if (-not [string]::IsNullOrWhiteSpace($LocalWorkspace) -and -not [string]::IsNullOrWhiteSpace($BooksWorkspace)) {
    throw "Use either -LocalWorkspace or -BooksWorkspace, not both."
}

$repoName = $null
$contentPrefix = $null

if ($LocalReview) {
    $checkout = Join-Path (Split-Path $repoRoot -Parent) 'artifacts\galactic-confederation-review'
    if (-not (Test-Path (Join-Path $checkout '.git'))) {
        New-Item -ItemType Directory -Force -Path (Split-Path $checkout -Parent) | Out-Null
        gh repo clone frankhaugen/galactic-confederation-review $checkout
    } else {
        git -C $checkout pull --ff-only
    }
    $LocalWorkspace = $checkout
    $repoName = 'galactic-confederation-review'
    $contentPrefix = 'docs/'
}
elseif (-not [string]::IsNullOrWhiteSpace($BooksWorkspace)) {
    $LocalWorkspace = $BooksWorkspace
    $repoName = 'books'
    $contentPrefix = 'src/'
}

if (-not [string]::IsNullOrWhiteSpace($LocalWorkspace)) {
    if (-not (Test-Path $LocalWorkspace)) {
        throw "Local workspace not found: $LocalWorkspace"
    }
    $env:BOOKSMOBILE_LOCAL_WORKSPACE = (Resolve-Path $LocalWorkspace).Path
    if ([string]::IsNullOrWhiteSpace($repoName)) {
        $repoName = 'galactic-confederation-review'
        $contentPrefix = 'docs/'
    }
    $env:BOOKSMOBILE_REPO_NAME = $repoName
    $env:BOOKSMOBILE_CONTENT_PREFIX = $contentPrefix
    Write-Host "Local workspace: $($env:BOOKSMOBILE_LOCAL_WORKSPACE) ($repoName)"
} else {
    Remove-Item Env:BOOKSMOBILE_LOCAL_WORKSPACE -ErrorAction SilentlyContinue
    if ([string]::IsNullOrWhiteSpace($ClientId)) {
        Write-Warning "BOOKSMOBILE_GITHUB_CLIENT_ID is empty. Sign-in will use the baked-in client id."
    } else {
        $env:BOOKSMOBILE_GITHUB_CLIENT_ID = $ClientId
    }
}

$env:BOOKSMOBILE_DEVICE = $Device
$project = Join-Path $repoRoot 'src/BooksMobile/BooksMobile.Desktop/BooksMobile.Desktop.csproj'
dotnet run --project $project -c Debug -p:NovolisUseProjectReferences=true -- --device $Device
