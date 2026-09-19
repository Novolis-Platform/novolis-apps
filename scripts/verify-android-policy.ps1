#Requires -Version 7.0
# Validates Android citizenship rules declared in build/apps.json against source manifests.
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Publish-NovolisApp.ps1')

$manifest = Get-NovolisAppsManifest -RepoRoot $RepoRoot
$errors = [System.Collections.Generic.List[string]]::new()

foreach ($app in $manifest.apps) {
    if (-not $app.android) { continue }

    $androidProj = Join-Path $RepoRoot ($app.android.project -replace '/', [IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $androidProj)) {
        $errors.Add("Missing Android project for $($app.key): $($app.android.project)")
        continue
    }

    $projDir = Split-Path $androidProj -Parent
    $xmlCandidates = @(
        Join-Path $projDir 'AndroidManifest.xml'
        Join-Path $projDir 'Platforms/Android/AndroidManifest.xml'
    )
    $xmlPath = $xmlCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $xmlPath) {
        $errors.Add("No AndroidManifest.xml for $($app.key)")
        continue
    }

    [xml]$xml = Get-Content -LiteralPath $xmlPath -Raw
    $ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
    $ns.AddNamespace('android', 'http://schemas.android.com/apk/res/android')

    $allowBackup = $xml.SelectSingleNode('//application/@android:allowBackup', $ns)?.Value
    if ($app.android.allowBackup -eq $false -and $allowBackup -eq 'true') {
        $errors.Add("$($app.key): allowBackup must be false (found true in $xmlPath)")
    }

    $cleartext = $xml.SelectSingleNode('//application/@android:usesCleartextTraffic', $ns)?.Value
    if ($app.android.usesCleartextTraffic -eq $false -and $cleartext -eq 'true') {
        $errors.Add("$($app.key): usesCleartextTraffic must be false")
    }

    $allowed = @($app.android.permissionAllowlist)
    $permissions = @($xml.SelectNodes('//uses-permission', $ns) | ForEach-Object {
        $_.GetAttribute('name', 'http://schemas.android.com/apk/res/android')
    } | Where-Object { $_ })

    # Ignore tools:node=remove entries — they revoke permissions.
    $active = @()
    foreach ($node in @($xml.SelectNodes('//uses-permission', $ns))) {
        $remove = $node.GetAttribute('node', 'http://schemas.android.com/tools')
        if ($remove -eq 'remove') { continue }
        $name = $node.GetAttribute('name', 'http://schemas.android.com/apk/res/android')
        if ($name) { $active += $name }
    }

    foreach ($perm in $active) {
        if ($allowed -notcontains $perm) {
            $errors.Add("$($app.key): permission '$perm' is not in allowlist [$($allowed -join ', ')]")
        }
    }

    $csproj = Get-Content -LiteralPath $androidProj -Raw
    if ($csproj -notmatch [regex]::Escape($app.android.applicationId)) {
        $errors.Add("$($app.key): applicationId '$($app.android.applicationId)' not found in project file")
    }

    if ($app.stack -ne 'maui') {
        $icon = $xml.SelectSingleNode('//application/@android:icon', $ns)?.Value
        if (-not $icon) {
            $errors.Add("$($app.key): AndroidManifest application is missing android:icon")
        }
    }

    if ($app.android.versionCodeStrategy -eq 'release-run' -and $csproj -match '<ApplicationVersion>\s*1\s*</ApplicationVersion>' -and $csproj -notmatch 'NovolisAndroidVersionCode') {
        $errors.Add("$($app.key): static ApplicationVersion=1 without NovolisAndroidVersionCode override path")
    }
}

# Version code helper sanity
$code = Get-NovolisAndroidVersionCode -PackageVersion '2026.1.0.42' -RunNumber 42
if ($code -lt 2026000000) {
    $errors.Add("Get-NovolisAndroidVersionCode produced unexpected value: $code")
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "Android policy OK for $(($manifest.apps | Where-Object android).Count) Android apps."
