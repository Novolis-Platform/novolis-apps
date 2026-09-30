#Requires -Version 7.0
# Shared publish + zip + Inno script generation for novolis-apps. Catalog: build/apps.json.

function Get-NovolisMsBuildPropertyArgs {
    param(
        [Parameter(Mandatory)]
        $Properties
    )
    $quote = [string][char]34
    $backslash = [string][char]92
    foreach ($entry in $Properties.GetEnumerator()) {
        $val = [string]$entry.Value
        $needsQuotes = $val.Contains(';') -or $val.Contains($quote) -or $val.Contains(' ')
        if ($needsQuotes) {
            $escaped = $val.Replace($quote, $backslash + $quote)
            '-p:' + $entry.Key + '=' + $quote + $escaped + $quote
        }
        else {
            '-p:' + $entry.Key + '=' + $val
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
        $windows = $app | Select-Object -ExpandProperty windows -ErrorAction SilentlyContinue
        if ($null -eq $windows) {
            throw ('App ' + $app.key + ' ships windows-inno without windows metadata.')
        }
        $row = New-Object System.Collections.Hashtable
        $row['Key'] = $app.key
        $row['Choice'] = $app.choice
        $row['Project'] = $app.projects.publishWindows
        $row['DisplayName'] = $app.displayName
        $row['AppId'] = $windows.appId
        $row['ExeName'] = $windows.exeName
        $row['GroupName'] = $windows.groupName
        $row['InstallDir'] = $windows.installDir
        $row['SetupBase'] = $windows.setupBase
        $row['ScriptFile'] = $windows.scriptFile
        $row['CloseApplicationsFilter'] = $windows.closeApplicationsFilter
        $row['AdditionalProjects'] = @($windows.additionalProjects)
        $row['Ship'] = @($app.ship)
        $row['Stack'] = $app.stack
        New-Object PSObject -Property $row
    }
}

function Get-NovolisAndroidAppCatalog {
    param([string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path)

    $manifest = Get-NovolisAppsManifest -RepoRoot $RepoRoot
    foreach ($app in $manifest.apps) {
        $shipsApk = $app.ship -contains 'android-apk'
        $playEnabled = $false
        $release = $app | Select-Object -ExpandProperty release -ErrorAction SilentlyContinue
        if ($release -and $release.googlePlay) {
            $playEnabled = [bool]$release.googlePlay.enabled
        }
        if (-not ($shipsApk -or $playEnabled)) { continue }
        $android = $app | Select-Object -ExpandProperty android -ErrorAction SilentlyContinue
        if ($null -eq $android) {
            throw ('App ' + $app.key + ' declares Android delivery without android metadata.')
        }
        $row = New-Object System.Collections.Hashtable
        $row['Key'] = $app.key
        $row['Choice'] = $app.choice
        $row['Project'] = $android.project
        $row['DisplayName'] = $app.displayName
        $row['ApplicationId'] = $android.applicationId
        $row['ArtifactPrefix'] = $app.artifactPrefix
        $row['SigningSecretKey'] = $android.signingSecretKey
        $row['Stack'] = $app.stack
        $row['IsMaui'] = ($app.stack -eq 'maui')
        $row['GooglePlayEnabled'] = $playEnabled
        New-Object PSObject -Property $row
    }
}

function Get-NovolisLinuxAppCatalog {
    param([string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path)

    $manifest = Get-NovolisAppsManifest -RepoRoot $RepoRoot
    foreach ($app in $manifest.apps) {
        if (-not ($app.ship -contains 'linux-tar')) { continue }
        $linux = $app | Select-Object -ExpandProperty linux -ErrorAction SilentlyContinue
        if ($null -eq $linux) {
            throw ('App ' + $app.key + ' ships linux-tar without Linux metadata.')
        }
        $row = New-Object System.Collections.Hashtable
        $row['Key'] = $app.key
        $row['Choice'] = $app.choice
        $row['Project'] = $linux.project
        $row['ExeName'] = $linux.exeName
        $row['DisplayName'] = $app.displayName
        $row['ArtifactPrefix'] = $app.artifactPrefix
        $row['Stack'] = $app.stack
        New-Object PSObject -Property $row
    }
}

function Get-NovolisReleaseCommit {
    param([Parameter(Mandatory)][string]$RepoRoot)

    if ($env:GITHUB_SHA) { return $env:GITHUB_SHA.Trim() }

    $sha = & git -C $RepoRoot rev-parse HEAD 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    $sha = "$sha".Trim()
    if ($sha.Length -lt 7) { return $null }
    return $sha
}

function Write-NovolisReleaseStamp {
    param(
        [Parameter(Mandatory)][string]$PublishDir,
        [Parameter(Mandatory)]$App,
        [Parameter(Mandatory)][string]$PackageVersion,
        [string]$Commit
    )

    if (-not $App) { throw "Release stamp requires catalog metadata." }
    $builtAt = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
    $stamp = New-Object System.Collections.Hashtable
    $stamp['name'] = $App.DisplayName
    $stamp['appId'] = $App.AppId
    $stamp['version'] = $PackageVersion
    $stamp['channel'] = 'windows-inno'
    $stamp['repository'] = 'https://github.com/Novolis-Platform/novolis-apps'
    $stamp['release'] = "https://github.com/Novolis-Platform/novolis-apps/releases/tag/v$PackageVersion"
    $stamp['builtAt'] = $builtAt
    if ($Commit) { $stamp['commit'] = $Commit }
    $json = $stamp | ConvertTo-Json
    [IO.File]::WriteAllText(
        (Join-Path $PublishDir 'Novolis.Release.json'),
        $json + "`n",
        [Text.UTF8Encoding]::new($false))
}

function Add-NovolisInstallerTrace {
    param(
        [Parameter(Mandatory)][string]$ScriptPath,
        [Parameter(Mandatory)][string]$DisplayName,
        [Parameter(Mandatory)][string]$PackageVersion,
        [string]$Commit
    )

    $contents = [IO.File]::ReadAllText($ScriptPath)
    if ($contents.Contains('AppComments=')) { return }
    $newLine = if ($contents.Contains("`r`n")) { "`r`n" } else { "`n" }
    $marker = "AppVersion=$PackageVersion"
    $index = $contents.IndexOf($marker)
    if ($index -lt 0) {
        throw "Installer script is missing AppVersion: $ScriptPath"
    }
    $insertAt = $index + $marker.Length
    if ($insertAt -lt $contents.Length -and $contents[$insertAt] -eq "`r") { $insertAt++ }
    if ($insertAt -lt $contents.Length -and $contents[$insertAt] -eq "`n") { $insertAt++ }
    $shortCommit = if ($Commit -and $Commit.Length -ge 12) { $Commit.Substring(0, 12) } else { $Commit }
    $comment = if ($shortCommit) { "$DisplayName $PackageVersion windows-inno $shortCommit" } else { "$DisplayName $PackageVersion windows-inno" }
    $productText = if ($Commit) { "$PackageVersion+$Commit" } else { $PackageVersion }
    $block = "AppComments=$comment$newLine" + "VersionInfoProductTextVersion=$productText$newLine"
    $contents = $contents.Insert($insertAt, $block)
    [IO.File]::WriteAllText($ScriptPath, $contents, [Text.UTF8Encoding]::new($false))
}

function Add-NovolisReachInstallerEntries {
    param([Parameter(Mandatory)][string]$ScriptPath)

    $contents = [IO.File]::ReadAllText($ScriptPath)
    $newLine = if ($contents.Contains("`r`n")) { "`r`n" } else { "`n" }
    $quote = [string][char]34
    $braceOpen = [string][char]123
    $braceClose = [string][char]125
    $appConst = $braceOpen + 'app' + $braceClose
    $userStartupConst = $braceOpen + 'userstartup' + $braceClose
    $serviceExe = $appConst + '\Novolis.Reach.Host.Windows.Service.exe'
    $serviceStartup = 'Name: ' + $quote + $userStartupConst + '\Novolis Reach Service' + $quote +
        '; Filename: ' + $quote + $serviceExe + $quote +
        '; WorkingDir: ' + $quote + $appConst + $quote
    $serviceRun = 'Filename: ' + $quote + $serviceExe + $quote +
        '; Description: ' + $quote + 'Start Novolis Reach Service' + $quote +
        '; Flags: nowait runhidden skipifsilent'

    $iconsSection = '[' + 'Icons]' + $newLine
    $runSection = '[' + 'Run]' + $newLine
    if (-not $contents.Contains($iconsSection)) {
        throw ('Reach installer script is missing the Icons section: ' + $ScriptPath)
    }
    if (-not $contents.Contains($runSection)) {
        throw ('Reach installer script is missing the Run section: ' + $ScriptPath)
    }

    $contents = $contents.Replace($iconsSection, $iconsSection + $serviceStartup + $newLine)
    $contents = $contents.Replace($runSection, $runSection + $serviceRun + $newLine)
    [IO.File]::WriteAllText(
        $ScriptPath,
        $contents,
        [Text.UTF8Encoding]::new($false))
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

    $commit = Get-NovolisReleaseCommit -RepoRoot $RepoRoot
    $informational = if ($commit) { "$PackageVersion+$commit" } else { $PackageVersion }
    $versionArgs = @(
        "-p:PackageVersion=$PackageVersion"
        "-p:AssemblyVersion=$AssemblyVersion"
        "-p:FileVersion=$FileVersion"
        "-p:InformationalVersion=$informational"
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

    $catalog = Get-NovolisAppCatalog -RepoRoot $RepoRoot | Where-Object { $_.Key -eq $AppKey } | Select-Object -First 1
    foreach ($extraProjectRelativePath in @($catalog.AdditionalProjects)) {
        if ([string]::IsNullOrWhiteSpace($extraProjectRelativePath)) { continue }
        $extraProject = Join-Path $RepoRoot $extraProjectRelativePath
        Write-Host "Publishing $AppKey host component $extraProjectRelativePath..."
        & dotnet restore $extraProject -r win-x64 @cfgArgs @versionArgs | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Restore failed for $extraProjectRelativePath." }
        & dotnet publish $extraProject `
            -c Release `
            -r win-x64 `
            --self-contained true `
            --no-restore `
            -o $publishDir `
            @versionArgs | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Publish failed for $extraProjectRelativePath." }
    }

    $exeBase = [System.IO.Path]::GetFileNameWithoutExtension($appProject)
    if ($catalog -and (Test-Path (Join-Path $publishDir $catalog.ExeName))) {
        $zipStem = [System.IO.Path]::GetFileNameWithoutExtension($catalog.ExeName)
    }
    else {
        $zipStem = $exeBase
    }

    Write-NovolisReleaseStamp -PublishDir $publishDir -App $catalog -PackageVersion $PackageVersion -Commit $commit

    $zipName = "$zipStem-$PackageVersion-win-x64.zip"
    $zipPath = Join-Path $stagingDir $zipName
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath | Out-Null
    Write-Host "Portable zip: $zipPath"

    $result = New-Object System.Collections.Hashtable
    $result['AppKey'] = $AppKey
    $result['ZipPath'] = $zipPath
    $result['ZipName'] = $zipName
    $result['InstallerPath'] = $null
    $result['InstallerName'] = $null

    if ($SkipInstaller) {
        return (New-Object PSObject -Property $result)
    }

    $inno = Get-NovolisAppInnoProfile -AppKey $AppKey -PackageVersion $PackageVersion -PublishDir $publishDir -InstallerDir $installerDir -RepoRoot $RepoRoot
    & dotnet msbuild $appProject `
        -t:NovolisGenerateInnoScript `
        @(Get-NovolisMsBuildPropertyArgs -Properties $inno.MsBuildArgs) | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Generating the Inno script failed with exit code $LASTEXITCODE." }
    Add-NovolisInstallerTrace -ScriptPath $inno.ScriptPath -DisplayName $catalog.DisplayName -PackageVersion $PackageVersion -Commit $commit
    if ($AppKey -eq 'reach') {
        Add-NovolisReachInstallerEntries -ScriptPath $inno.ScriptPath
    }

    $programFilesX86 = [Environment]::GetFolderPath('ProgramFilesX86')
    $iscc = @(
        (Join-Path $programFilesX86 'Inno Setup 6\ISCC.exe')
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if (-not $iscc) {
        $iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1)
    }
    if (-not $iscc) {
        Write-Warning ('ISCC.exe not found. Inno script written to ' + $inno.ScriptPath + ' - install Inno Setup 6 to compile the installer.')
        return (New-Object PSObject -Property $result)
    }

    & $iscc $inno.ScriptPath | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "ISCC failed with exit code $LASTEXITCODE." }
    if (-not (Test-Path $inno.InstallerPath)) {
        throw ('Expected installer not found: ' + $inno.InstallerPath)
    }

    $result.InstallerPath = $inno.InstallerPath
    $result.InstallerName = Split-Path $inno.InstallerPath -Leaf
    Write-Host ('Installer: ' + $inno.InstallerPath)
    return (New-Object PSObject -Property $result)
}

function Publish-NovolisLinuxApp {
    param(
        [Parameter(Mandatory)][string]$RepoRoot,
        [Parameter(Mandatory)][string]$AppKey,
        [Parameter(Mandatory)][string]$ProjectRelativePath,
        [Parameter(Mandatory)][string]$PackageVersion,
        [Parameter(Mandatory)][string]$AssemblyVersion,
        [Parameter(Mandatory)][string]$FileVersion
    )

    $ErrorActionPreference = 'Stop'
    $app = Get-NovolisLinuxAppCatalog -RepoRoot $RepoRoot |
        Where-Object { $_.Key -eq $AppKey } |
        Select-Object -First 1
    if (-not $app) { throw "Unknown linux-tar app key: $AppKey" }

    $project = Join-Path $RepoRoot $ProjectRelativePath
    $stagingDir = Join-Path $RepoRoot "artifacts/$AppKey/linux"
    $publishDir = Join-Path $stagingDir 'app'
    New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

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

    Write-Host "Publishing $AppKey $PackageVersion (linux-x64)..."
    & dotnet restore $project -r linux-x64 @cfgArgs @versionArgs | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Restore failed with exit code $LASTEXITCODE." }

    & dotnet publish $project `
        -c Release `
        -r linux-x64 `
        --self-contained true `
        --no-restore `
        -o $publishDir `
        @versionArgs | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE." }

    $executable = Join-Path $publishDir $app.ExeName
    if (-not (Test-Path -LiteralPath $executable)) {
        throw "Expected Linux executable not found: $executable"
    }

    $tarName = $app.ArtifactPrefix + '-' + $PackageVersion + '-linux-x64.tar.gz'
    $tarPath = Join-Path $stagingDir $tarName
    if (Test-Path -LiteralPath $tarPath) {
        Remove-Item -LiteralPath $tarPath -Force
    }

    & tar -C $publishDir -czf $tarPath .
    if ($LASTEXITCODE -ne 0) { throw "tar failed with exit code $LASTEXITCODE." }
    Write-Host "Linux tarball: $tarPath"

    $artifact = New-Object System.Collections.Hashtable
    $artifact['AppKey'] = $AppKey
    $artifact['TarPath'] = $tarPath
    $artifact['TarName'] = $tarName
    return (New-Object PSObject -Property $artifact)
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
    $setupBase = $app.SetupBase + '-' + $PackageVersion + '-win-x64'
    $license = Join-Path $RepoRoot 'LICENSE'
    $icon = Join-Path $RepoRoot 'icon.ico'

    $msbuild = New-Object System.Collections.Hashtable
    $msbuild['NovolisInnoAppName'] = $app.DisplayName
    $msbuild['NovolisInnoAppVersion'] = $PackageVersion
    $msbuild['NovolisInnoPublishDir'] = $PublishDir
    $msbuild['NovolisInnoAppExeName'] = $app.ExeName
    $msbuild['NovolisInnoOutputDir'] = $InstallerDir
    $msbuild['NovolisInnoAppId'] = $app.AppId
    $msbuild['NovolisInnoDefaultGroupName'] = $app.GroupName
    $msbuild['NovolisInnoOutputBaseFilename'] = $setupBase
    $msbuild['NovolisInnoInstallDirName'] = $app.InstallDir
    $msbuild['NovolisInnoScriptPath'] = $script
    $msbuild['NovolisInnoAppPublisher'] = 'Novolis'
    $msbuild['NovolisInnoAppPublisherURL'] = 'https://github.com/Novolis-Platform'
    $msbuild['NovolisInnoAppCopyright'] = 'Copyright (c) Novolis'
    $msbuild['NovolisInnoVersionInfoCompany'] = 'Novolis'
    $msbuild['NovolisInnoVersionInfoDescription'] = $app.DisplayName + ' - Novolis'
    $msbuild['NovolisInnoAppSupportURL'] = 'https://github.com/Novolis-Platform/novolis-apps/issues'
    $msbuild['NovolisInnoAppUpdatesURL'] = 'https://github.com/Novolis-Platform/novolis-apps/releases'
    $msbuild['NovolisInnoCloseApplicationsFilter'] = $app.CloseApplicationsFilter
    if (Test-Path -LiteralPath $license) {
        $msbuild['NovolisInnoLicenseFile'] = $license
    }
    if (Test-Path -LiteralPath $icon) {
        $msbuild['NovolisInnoSetupIconFile'] = $icon
    }

    $innoProfile = New-Object System.Collections.Hashtable
    $innoProfile['ScriptPath'] = $script
    $innoProfile['InstallerPath'] = Join-Path $InstallerDir ($setupBase + '.exe')
    $innoProfile['MsBuildArgs'] = $msbuild
    return (New-Object PSObject -Property $innoProfile)
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

    $keystore = New-Object System.Collections.Hashtable
    $keystore['Path'] = $Path
    $keystore['Alias'] = $alias
    $keystore['Password'] = $pass
    return (New-Object PSObject -Property $keystore)
}

function Find-NovolisPublishedAndroidArtifact {
    param(
        [Parameter(Mandatory)][string]$RepoRoot,
        [Parameter(Mandatory)][ValidateSet('apk', 'aab')][string]$PackageFormat,
        [string]$ApplicationId
    )

    $extension = $PackageFormat.ToLowerInvariant()
    @(Get-ChildItem -Path $RepoRoot -Recurse -File -Filter "*.$extension" -ErrorAction SilentlyContinue) |
        Where-Object {
            $_.FullName -notmatch '[\\/]obj[\\/]' -and
            $_.FullName -match '(?i)[\\/]release(?:[_\\/]|$)' -and
            $_.Name -notmatch '(?i)unsigned' -and
            ([string]::IsNullOrWhiteSpace($ApplicationId) -or
                $_.Name.Contains($ApplicationId, [StringComparison]::OrdinalIgnoreCase))
        } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
}

function Find-NovolisPublishedApk {
    param([Parameter(Mandatory)][string]$RepoRoot)
    Find-NovolisPublishedAndroidArtifact -RepoRoot $RepoRoot -PackageFormat apk
}

function Get-NovolisAndroidVersionCode {
    param(
        [Parameter(Mandatory)][string]$PackageVersion,
        [int]$RunNumber = 0
    )
    # YEAR.MAJOR.MINOR.BUILD → monotonic int; prefer run number as BUILD when present.
    # The four digit slots are YEAR / MAJOR / MINOR / BUILD with a base of
    # 1,000,000. This avoids the old build % 100 collision at release 100.
    $parts = $PackageVersion.Split('.')
    if ($parts.Length -lt 3) { throw "PackageVersion '$PackageVersion' is not YEAR.MAJOR.MINOR[.BUILD]." }
    $year = [int]$parts[0]
    $major = [int]$parts[1]
    $minor = [int]$parts[2]
    $build = if ($parts.Length -ge 4) { [int]$parts[3] } else { $RunNumber }
    if ($RunNumber -gt $build) { $build = $RunNumber }
    if ($year -lt 2000 -or $year -gt 2099) { throw "YEAR '$year' is outside the Android versionCode range 2000..2099." }
    if ($major -lt 0 -or $major -gt 9) { throw "MAJOR '$major' must fit the Android versionCode range 0..9." }
    if ($minor -lt 0 -or $minor -gt 999) { throw "MINOR '$minor' must fit the Android versionCode range 0..999." }
    if ($build -lt 0 -or $build -gt 999) { throw "BUILD '$build' must fit the Android versionCode range 0..999." }

    $code = ($year * 1000000) + ($major * 100000) + ($minor * 1000) + $build
    if ($code -gt 2100000000) {
        throw "Computed Android versionCode '$code' exceeds Android's 2100000000 limit."
    }
    return $code
}

function Publish-NovolisAndroidArtifact {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingPlainTextForPassword', 'KeystorePassword')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingPlainTextForPassword', 'KeyPassword')]
    param(
        [Parameter(Mandatory)][string]$RepoRoot,
        [Parameter(Mandatory)][string]$AppKey,
        [Parameter(Mandatory)][string]$PackageVersion,
        [int]$RunNumber = 0,
        [string]$KeystorePath,
        [string]$KeyAlias,
        [string]$KeystorePassword,
        [string]$KeyPassword,
        [ValidateSet('apk', 'aab')][string]$PackageFormat = 'apk',
        [switch]$RequirePersistentSigning
    )

    $ErrorActionPreference = 'Stop'
    $app = Get-NovolisAndroidAppCatalog -RepoRoot $RepoRoot | Where-Object { $_.Key -eq $AppKey } | Select-Object -First 1
    if (-not $app) { throw "Unknown Android app key: $AppKey" }

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
        "-p:AndroidPackageFormat=$PackageFormat"
        "-p:AndroidPackageFormats=$PackageFormat"
        '-p:AndroidBuildApplicationPackage=true'
        '-p:AndroidFastDeployment=false'
        '-p:EmbedAssembliesIntoApk=true'
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
        if ($RequirePersistentSigning) {
            throw "Persistent Android signing secrets are required for Google Play delivery of $AppKey."
        }
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

    Write-Host "Publishing Android $PackageFormat for $AppKey (versionCode=$versionCode)..."
    & dotnet publish @publishArgs @cfgArgs | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Android $PackageFormat publish failed with exit code $LASTEXITCODE." }

    $artifact = Find-NovolisPublishedAndroidArtifact `
        -RepoRoot $RepoRoot `
        -PackageFormat $PackageFormat `
        -ApplicationId $app.ApplicationId
    if (-not $artifact) { throw "No Android $PackageFormat produced for $AppKey." }

    $dest = Join-Path $stagingDir ($app.ArtifactPrefix + '-' + $PackageVersion + '-android.' + $PackageFormat)
    Copy-Item -LiteralPath $artifact.FullName -Destination $dest -Force
    Write-Host "Android $PackageFormat`: $dest"
    $published = New-Object System.Collections.Hashtable
    $published['AppKey'] = $AppKey
    $published['ApplicationId'] = $app.ApplicationId
    $published['ArtifactPath'] = $dest
    $published['ArtifactName'] = Split-Path $dest -Leaf
    $published['PackageFormat'] = $PackageFormat
    $published['VersionCode'] = $versionCode
    return (New-Object PSObject -Property $published)
}

function Publish-NovolisAndroidApk {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingPlainTextForPassword', 'KeystorePassword')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingPlainTextForPassword', 'KeyPassword')]
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

    $item = Publish-NovolisAndroidArtifact `
        -RepoRoot $RepoRoot `
        -AppKey $AppKey `
        -PackageVersion $PackageVersion `
        -RunNumber $RunNumber `
        -KeystorePath $KeystorePath `
        -KeyAlias $KeyAlias `
        -KeystorePassword $KeystorePassword `
        -KeyPassword $KeyPassword `
        -PackageFormat apk

    $apk = New-Object System.Collections.Hashtable
    $apk['AppKey'] = $item.AppKey
    $apk['ApplicationId'] = $item.ApplicationId
    $apk['ApkPath'] = $item.ArtifactPath
    $apk['ApkName'] = $item.ArtifactName
    $apk['VersionCode'] = $item.VersionCode
    return (New-Object PSObject -Property $apk)
}

function Publish-NovolisAndroidBundle {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingPlainTextForPassword', 'KeystorePassword')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingPlainTextForPassword', 'KeyPassword')]
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

    $item = Publish-NovolisAndroidArtifact `
        -RepoRoot $RepoRoot `
        -AppKey $AppKey `
        -PackageVersion $PackageVersion `
        -RunNumber $RunNumber `
        -KeystorePath $KeystorePath `
        -KeyAlias $KeyAlias `
        -KeystorePassword $KeystorePassword `
        -KeyPassword $KeyPassword `
        -PackageFormat aab `
        -RequirePersistentSigning

    $aab = New-Object System.Collections.Hashtable
    $aab['AppKey'] = $item.AppKey
    $aab['ApplicationId'] = $item.ApplicationId
    $aab['AabPath'] = $item.ArtifactPath
    $aab['AabName'] = $item.ArtifactName
    $aab['VersionCode'] = $item.VersionCode
    return (New-Object PSObject -Property $aab)
}
