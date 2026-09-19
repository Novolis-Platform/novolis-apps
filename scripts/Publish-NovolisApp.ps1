#Requires -Version 7.0
# Shared publish + zip + Inno script generation for novolis-apps. Catalog: build/apps.json.

function Get-NovolisMsBuildPropertyArgs {
    param([Parameter(Mandatory)][hashtable]$Properties)
    foreach ($entry in $Properties.GetEnumerator()) {
        $val = [string]$entry.Value
        if ($val -match '[;"]' -or $val.Contains(' ')) {
            "-p:$($entry.Key)=`"$($val.Replace('"','\"'))`""
        }
        else {
            "-p:$($entry.Key)=$val"
        }
    }
}

function Get-NovolisAppsManifestPath {
    param([Parameter(Mandatory)][string]$RepoRoot)
    Join-Path $RepoRoot 'build/apps.json'
}

function Get-NovolisAppsManifest {
    param([Parameter(Mandatory)][string]$RepoRoot)
    $path = Get-NovolisAppsManifestPath -RepoRoot $RepoRoot
    if (-not (Test-Path -LiteralPath $path)) {
        throw "App manifest not found: $path"
    }
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

function Get-NovolisAppCatalog {
    param([string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path)

    $manifest = Get-NovolisAppsManifest -RepoRoot $RepoRoot
    foreach ($app in $manifest.apps) {
        if (-not ($app.ship -contains 'windows-inno')) { continue }
        if (-not $app.windows) { throw "App $($app.key) ships windows-inno without windows metadata." }
        [pscustomobject]@{
            Key                        = $app.key
            Choice                     = $app.choice
            Project                    = $app.projects.publishWindows
            DisplayName                = $app.displayName
            AppId                      = $app.windows.appId
            ExeName                    = $app.windows.exeName
            GroupName                  = $app.windows.groupName
            InstallDir                 = $app.windows.installDir
            SetupBase                  = $app.windows.setupBase
            ScriptFile                 = $app.windows.scriptFile
            CloseApplicationsFilter    = $app.windows.closeApplicationsFilter
            Ship                       = @($app.ship)
            Stack                      = $app.stack
        }
    }
}

function Get-NovolisAndroidAppCatalog {
    param([string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path)

    $manifest = Get-NovolisAppsManifest -RepoRoot $RepoRoot
    foreach ($app in $manifest.apps) {
        if (-not ($app.ship -contains 'android-apk')) { continue }
        if (-not $app.android) { throw "App $($app.key) ships android-apk without android metadata." }
        [pscustomobject]@{
            Key               = $app.key
            Choice            = $app.choice
            Project           = $app.android.project
            DisplayName       = $app.displayName
            ApplicationId     = $app.android.applicationId
            ArtifactPrefix    = $app.artifactPrefix
            SigningSecretKey  = $app.android.signingSecretKey
            Stack             = $app.stack
            IsMaui            = ($app.stack -eq 'maui')
        }
    }
}

function Publish-NovolisApp {
    param(
        [Parameter(Mandatory)]
        [string]$RepoRoot,
        [Parameter(Mandatory)]
        [string]$AppKey,
        [Parameter(Mandatory)]
        [string]$ProjectRelativePath,
        [Parameter(Mandatory)]
        [string]$PackageVersion,
        [Parameter(Mandatory)]
        [string]$AssemblyVersion,
        [Parameter(Mandatory)]
        [string]$FileVersion,
        [switch]$SkipInstaller
    )

    $ErrorActionPreference = 'Stop'

    $appProject = Join-Path $RepoRoot $ProjectRelativePath
    $stagingDir = Join-Path $RepoRoot "artifacts/$AppKey"
    $publishDir = Join-Path $stagingDir 'app'
    $installerDir = Join-Path $stagingDir 'installer'

    New-Item -ItemType Directory -Force -Path $publishDir, $installerDir | Out-Null

    $versionArgs = @(
        "-p:PackageVersion=$PackageVersion"
        "-p:AssemblyVersion=$AssemblyVersion"
        "-p:FileVersion=$FileVersion"
        "-p:InformationalVersion=$PackageVersion"
    )

    $cfgArgs = @()
    $nugetConfig = Join-Path $RepoRoot 'nuget.config'
    if (Test-Path $nugetConfig) {
        $cfgArgs = @('--configfile', $nugetConfig)
    }

    Write-Host "Publishing $AppKey $PackageVersion (win-x64)..."
    & dotnet restore $appProject -r win-x64 @cfgArgs @versionArgs | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Restore failed with exit code $LASTEXITCODE." }

    if ($AppKey -eq 'live-studio') {
        foreach ($extra in @(
            'src/LiveStudio/host/LiveStudio.Host.csproj',
            'src/LiveStudio/launcher/LiveStudio.Launcher.csproj'
        )) {
            $extraProject = Join-Path $RepoRoot $extra
            & dotnet restore $extraProject -r win-x64 @cfgArgs @versionArgs | Out-Host
            if ($LASTEXITCODE -ne 0) { throw "Restore failed for $extra with exit code $LASTEXITCODE." }
        }
    }

    & dotnet publish $appProject `
        -c Release `
        -r win-x64 `
        --self-contained true `
        --no-restore `
        -o $publishDir `
        @versionArgs | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE." }

    $exeBase = [System.IO.Path]::GetFileNameWithoutExtension($appProject)
    $catalog = Get-NovolisAppCatalog -RepoRoot $RepoRoot | Where-Object { $_.Key -eq $AppKey } | Select-Object -First 1
    if ($catalog -and (Test-Path (Join-Path $publishDir $catalog.ExeName))) {
        $zipStem = [System.IO.Path]::GetFileNameWithoutExtension($catalog.ExeName)
    }
    else {
        $zipStem = $exeBase
    }

    $zipName = "$zipStem-$PackageVersion-win-x64.zip"
    $zipPath = Join-Path $stagingDir $zipName
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath | Out-Null
    Write-Host "Portable zip: $zipPath"

    $result = [ordered]@{
        AppKey        = $AppKey
        ZipPath       = $zipPath
        ZipName       = $zipName
        InstallerPath = $null
        InstallerName = $null
    }

    if ($SkipInstaller) {
        return [pscustomobject]$result
    }

    $inno = Get-NovolisAppInnoProfile -AppKey $AppKey -PackageVersion $PackageVersion -PublishDir $publishDir -InstallerDir $installerDir -RepoRoot $RepoRoot
    & dotnet msbuild $appProject `
        -t:NovolisGenerateInnoScript `
        @(Get-NovolisMsBuildPropertyArgs -Properties $inno.MsBuildArgs) | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Generating the Inno script failed with exit code $LASTEXITCODE." }

    $iscc = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if (-not $iscc) {
        $iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1)
    }
    if (-not $iscc) {
        Write-Warning "ISCC.exe not found. Inno script written to $($inno.ScriptPath) — install Inno Setup 6 to compile the installer."
        return [pscustomobject]$result
    }

    & $iscc $inno.ScriptPath | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "ISCC failed with exit code $LASTEXITCODE." }
    if (-not (Test-Path $inno.InstallerPath)) {
        throw "Expected installer not found: $($inno.InstallerPath)"
    }

    $result.InstallerPath = $inno.InstallerPath
    $result.InstallerName = Split-Path $inno.InstallerPath -Leaf
    Write-Host "Installer: $($inno.InstallerPath)"
    return [pscustomobject]$result
}

function Get-NovolisAppInnoProfile {
    param(
        [Parameter(Mandatory)][string]$AppKey,
        [Parameter(Mandatory)][string]$PackageVersion,
        [Parameter(Mandatory)][string]$PublishDir,
        [Parameter(Mandatory)][string]$InstallerDir,
        [Parameter(Mandatory)][string]$RepoRoot
    )

    $app = Get-NovolisAppCatalog -RepoRoot $RepoRoot | Where-Object { $_.Key -eq $AppKey } | Select-Object -First 1
    if (-not $app) { throw "Unknown app key: $AppKey" }

    $script = Join-Path $InstallerDir $app.ScriptFile
    $setupBase = "$($app.SetupBase)-$PackageVersion-win-x64"
    $license = Join-Path $RepoRoot 'LICENSE'
    $icon = Join-Path $RepoRoot 'icon.ico'

    $msbuild = @{
        NovolisInnoAppName                   = $app.DisplayName
        NovolisInnoAppVersion                = $PackageVersion
        NovolisInnoPublishDir                = $PublishDir
        NovolisInnoAppExeName                = $app.ExeName
        NovolisInnoOutputDir                 = $InstallerDir
        NovolisInnoAppId                     = $app.AppId
        NovolisInnoDefaultGroupName          = $app.GroupName
        NovolisInnoOutputBaseFilename        = $setupBase
        NovolisInnoInstallDirName            = $app.InstallDir
        NovolisInnoScriptPath                = $script
        NovolisInnoAppPublisher              = 'Novolis'
        NovolisInnoAppPublisherURL           = 'https://github.com/Novolis-Platform'
        NovolisInnoAppCopyright              = 'Copyright (c) Novolis'
        NovolisInnoVersionInfoCompany        = 'Novolis'
        NovolisInnoVersionInfoDescription    = "$($app.DisplayName) - Novolis"
        NovolisInnoAppSupportURL             = 'https://github.com/Novolis-Platform/novolis-apps/issues'
        NovolisInnoAppUpdatesURL             = 'https://github.com/Novolis-Platform/novolis-apps/releases'
        NovolisInnoCloseApplicationsFilter   = $app.CloseApplicationsFilter
    }
    if (Test-Path -LiteralPath $license) {
        $msbuild['NovolisInnoLicenseFile'] = $license
    }
    if (Test-Path -LiteralPath $icon) {
        $msbuild['NovolisInnoSetupIconFile'] = $icon
    }

    return [pscustomobject]@{
        ScriptPath    = $script
        InstallerPath = Join-Path $InstallerDir "$setupBase.exe"
        MsBuildArgs   = $msbuild
    }
}

function Resolve-NovolisKeytool {
    $cmd = Get-Command keytool -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1
    if ($cmd) { return $cmd }

    if ($env:JAVA_HOME) {
        foreach ($name in @('keytool.exe', 'keytool')) {
            $candidate = Join-Path $env:JAVA_HOME "bin/$name"
            if (Test-Path -LiteralPath $candidate) { return $candidate }
        }
    }

    return $null
}

function New-NovolisAdhocAndroidKeystore {
    param([Parameter(Mandatory)][string]$Path)

    $keytool = Resolve-NovolisKeytool
    if (-not $keytool) {
        throw "keytool not found. Install a JDK 17+ to produce an installable Android APK without ANDROID_KEYSTORE_* secrets."
    }

    $alias = 'novolis-adhoc'
    $pass = 'novolis-adhoc-release'
    $dir = Split-Path -Parent $Path
    if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    if (Test-Path -LiteralPath $Path) { Remove-Item -LiteralPath $Path -Force }

    & $keytool -genkeypair `
        -keystore $Path `
        -alias $alias `
        -keyalg RSA `
        -keysize 2048 `
        -validity 10000 `
        -storepass $pass `
        -keypass $pass `
        -dname 'CN=Novolis Ad Hoc Release,O=Novolis,C=NO'
    if ($LASTEXITCODE -ne 0) { throw "keytool failed with exit code $LASTEXITCODE." }

    [pscustomobject]@{
        Path     = $Path
        Alias    = $alias
        Password = $pass
    }
}

function Find-NovolisPublishedApk {
    param([Parameter(Mandatory)][string]$RepoRoot)

    $files = @(Get-ChildItem -Path $RepoRoot -Recurse -File -Filter '*-Signed.apk' -ErrorAction SilentlyContinue)
    if ($files.Count -eq 0) {
        $files = @(Get-ChildItem -Path $RepoRoot -Recurse -File -Filter '*.apk' -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -notmatch 'unsigned' })
    }

    $files |
        Where-Object {
            $_.FullName -notmatch '[\\/]obj[\\/]' -and
            $_.FullName -match '(?i)[\\/]release'
        } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
}

function Get-NovolisAndroidVersionCode {
    param(
        [Parameter(Mandatory)][string]$PackageVersion,
        [int]$RunNumber = 0
    )
    # YEAR.MAJOR.MINOR.BUILD → monotonic int; prefer run number as BUILD when present.
    $parts = $PackageVersion.Split('.')
    if ($parts.Length -lt 3) { throw "PackageVersion '$PackageVersion' is not YEAR.MAJOR.MINOR[.BUILD]." }
    $year = [int]$parts[0]
    $major = [int]$parts[1]
    $minor = [int]$parts[2]
    $build = if ($parts.Length -ge 4) { [int]$parts[3] } else { $RunNumber }
    if ($RunNumber -gt $build) { $build = $RunNumber }
    # Fits comfortably under Android's 2100000000 limit for CalVer through 2099.
    return ($year * 1000000) + ($major * 10000) + ($minor * 100) + ($build % 100)
}

function Publish-NovolisAndroidApk {
    param(
        [Parameter(Mandatory)][string]$RepoRoot,
        [Parameter(Mandatory)][string]$AppKey,
        [Parameter(Mandatory)][string]$PackageVersion,
        [int]$RunNumber = 0,
        [string]$KeystorePath,
        [string]$KeyAlias,
        [string]$KeystorePassword,
        [string]$KeyPassword
    )

    $ErrorActionPreference = 'Stop'
    $app = Get-NovolisAndroidAppCatalog -RepoRoot $RepoRoot | Where-Object { $_.Key -eq $AppKey } | Select-Object -First 1
    if (-not $app) { throw "Unknown android-apk app key: $AppKey" }

    $project = Join-Path $RepoRoot $app.Project
    $stagingDir = Join-Path $RepoRoot "artifacts/$AppKey/android"
    New-Item -ItemType Directory -Force -Path $stagingDir | Out-Null

    $versionCode = Get-NovolisAndroidVersionCode -PackageVersion $PackageVersion -RunNumber $RunNumber
    $cfgArgs = @()
    $nugetConfig = Join-Path $RepoRoot 'nuget.config'
    if (Test-Path $nugetConfig) { $cfgArgs = @('--configfile', $nugetConfig) }

    $publishArgs = @(
        $project
        '-f', 'net10.0-android'
        '-c', 'Release'
        '-p:AndroidPackageFormats=apk'
        "-p:ApplicationDisplayVersion=$PackageVersion"
        "-p:ApplicationVersion=$versionCode"
        "-p:NovolisAndroidVersionCode=$versionCode"
        "-p:PackageVersion=$PackageVersion"
    )
    if ($app.IsMaui) {
        $publishArgs += '-p:NovolisMauiTargetFrameworks=net10.0-android'
    }

    $hasPersistentKey = $KeystorePath -and $KeyAlias -and $KeystorePassword -and $KeyPassword
    if (-not $hasPersistentKey) {
        Write-Warning "No persistent Android signing key for $AppKey. Generating an adhoc keystore for sideload testing — not upgrade-safe."
        $adhoc = New-NovolisAdhocAndroidKeystore -Path (Join-Path ([IO.Path]::GetTempPath()) "novolis-adhoc-$AppKey.keystore")
        $KeystorePath = $adhoc.Path
        $KeyAlias = $adhoc.Alias
        $KeystorePassword = $adhoc.Password
        $KeyPassword = $adhoc.Password
    }

    $publishArgs += @(
        '-p:AndroidKeyStore=true'
        "-p:AndroidSigningKeyStore=$KeystorePath"
        "-p:AndroidSigningKeyAlias=$KeyAlias"
        "-p:AndroidSigningStorePass=$KeystorePassword"
        "-p:AndroidSigningKeyPass=$KeyPassword"
    )

    Write-Host "Publishing Android APK for $AppKey (versionCode=$versionCode)..."
    & dotnet publish @publishArgs @cfgArgs | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Android publish failed with exit code $LASTEXITCODE." }

    $apk = Find-NovolisPublishedApk -RepoRoot $RepoRoot
    if (-not $apk) { throw "No APK produced for $AppKey." }

    $dest = Join-Path $stagingDir "$($app.ArtifactPrefix)-$PackageVersion-android.apk"
    Copy-Item -LiteralPath $apk.FullName -Destination $dest -Force
    Write-Host "APK: $dest"
    return [pscustomobject]@{
        AppKey      = $AppKey
        ApkPath     = $dest
        ApkName     = Split-Path $dest -Leaf
        VersionCode = $versionCode
    }
}
