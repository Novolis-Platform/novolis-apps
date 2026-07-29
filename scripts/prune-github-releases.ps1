#Requires -Version 7.0
# Keep the newest N GitHub Releases for novolis-apps; delete older tags/releases.
param(
    [string]$Repo = 'Novolis-Platform/novolis-apps',
    [ValidateRange(1, 50)]
    [int]$Keep = 5,
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'

$releases = gh release list --repo $Repo --limit 100 --json tagName,isLatest,createdAt |
    ConvertFrom-Json |
    Sort-Object { [datetime]$_.createdAt } -Descending

if ($releases.Count -eq 0) {
    Write-Host "No releases in $Repo"
    return
}

$keepList = @($releases | Select-Object -First $Keep)
$drop = @($releases | Select-Object -Skip $Keep)

Write-Host "Keeping $($keepList.Count): $($keepList.tagName -join ', ')"
if ($drop.Count -eq 0) {
    Write-Host 'Nothing to prune.'
    return
}

foreach ($r in $drop) {
    if ($WhatIf) {
        Write-Host "WhatIf: would delete $($r.tagName)"
        continue
    }
    Write-Host "Deleting $($r.tagName)..."
    gh release delete $r.tagName --repo $Repo --yes --cleanup-tag
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to delete release $($r.tagName) (exit $LASTEXITCODE)."
    }
}

Write-Host "Pruned $($drop.Count) release(s)."
