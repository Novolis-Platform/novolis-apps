#Requires -Version 7.0
# Shared publish + zip + Inno script generation for novolis-apps WinExe/Exe projects.

function Get-NovolisAppCatalog {
    @(
        [pscustomobject]@{
            Key           = 'books-writer-studio'
            Choice        = 'BooksWriterStudio'
            Project       = 'src/BooksWriterStudio/BooksWriterStudio.csproj'
            DisplayName   = 'Books Writer Studio'
            AppId         = 'Novolis.BooksWriterStudio'
            ExeName       = 'BooksWriterStudio.exe'
            GroupName     = 'Books Writer Studio'
            InstallDir    = 'Novolis\Books Writer Studio'
            SetupBase     = 'BooksWriterStudioSetup'
            ScriptFile    = 'books-writer-studio.iss'
        }
        [pscustomobject]@{
            Key           = 'draft-studio'
            Choice        = 'DraftStudio'
            Project       = 'src/DraftStudio/DraftStudio.csproj'
            DisplayName   = 'Draft Studio'
            AppId         = 'Novolis.DraftStudio'
            ExeName       = 'DraftStudio.exe'
            GroupName     = 'Draft Studio'
            InstallDir    = 'Novolis\Draft Studio'
            SetupBase     = 'DraftStudioSetup'
            ScriptFile    = 'draft-studio.iss'
        }
        [pscustomobject]@{
            Key           = 'cad-studio-3d'
            Choice        = 'CadStudio3D'
            Project       = 'src/CadStudio3D/CadStudio3D.csproj'
            DisplayName   = 'Novolis CAD Studio 3D'
            AppId         = 'Novolis.CadStudio3D'
            ExeName       = 'CadStudio3D.exe'
            GroupName     = 'Novolis CAD Studio 3D'
            InstallDir    = 'Novolis\CAD Studio 3D'
            SetupBase     = 'CadStudio3DSetup'
            ScriptFile    = 'cad-studio-3d.iss'
        }
        [pscustomobject]@{
            Key           = 'sketch-studio'
            Choice        = 'SketchStudio'
            Project       = 'src/SketchStudio/SketchStudio.csproj'
            DisplayName   = 'Sketch Studio'
            AppId         = 'Novolis.SketchStudio'
            ExeName       = 'SketchStudio.exe'
            GroupName     = 'Sketch Studio'
            InstallDir    = 'Novolis\Sketch Studio'
            SetupBase     = 'SketchStudioSetup'
            ScriptFile    = 'sketch-studio.iss'
        }
        [pscustomobject]@{
            Key           = 'sins-of-a-capitalism-tycoon'
            Choice        = 'SinsOfACapitalismTycoon'
            Project       = 'src/SinsOfACapitalismTycoon/SinsOfACapitalismTycoon.csproj'
            DisplayName   = 'Sins of a Capitalism Tycoon'
            AppId         = 'Novolis.SinsOfACapitalismTycoon'
            ExeName       = 'SinsOfACapitalismTycoon.exe'
            GroupName     = 'Sins of a Capitalism Tycoon'
            InstallDir    = 'Novolis\Sins of a Capitalism Tycoon'
            SetupBase     = 'SinsOfACapitalismTycoonSetup'
            ScriptFile    = 'sins-of-a-capitalism-tycoon.iss'
        }
        [pscustomobject]@{
            Key           = 'live-studio'
            Choice        = 'LiveStudio'
            Project       = 'src/LiveStudio/studio/LiveStudio.csproj'
            DisplayName   = 'Live Studio'
            AppId         = 'Novolis.Audio.Live.Studio'
            ExeName       = 'Novolis.Audio.Live.Studio.exe'
            GroupName     = 'Live Studio'
            InstallDir    = 'Novolis\Live Studio'
            SetupBase     = 'LiveStudioSetup'
            ScriptFile    = 'live-studio.iss'
        }
    )
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

    # Live Studio bundles host + launcher after publish; restore them so nested Publish finds assets.
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
    # Prefer AssemblyName-driven exe when present in publish output (e.g. Live Studio).
    $catalog = Get-NovolisAppCatalog | Where-Object { $_.Key -eq $AppKey } | Select-Object -First 1
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
        @($inno.MsBuildArgs.GetEnumerator() | ForEach-Object { "-p:$($_.Key)=$($_.Value)" }) | Out-Host
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

    $app = Get-NovolisAppCatalog | Where-Object { $_.Key -eq $AppKey } | Select-Object -First 1
    if (-not $app) { throw "Unknown app key: $AppKey" }

    $script = Join-Path $InstallerDir $app.ScriptFile
    $setupBase = "$($app.SetupBase)-$PackageVersion-win-x64"
    $license = Join-Path $RepoRoot 'LICENSE'
    $icon = Join-Path $RepoRoot 'icon.ico'

    $msbuild = @{
        NovolisInnoAppName                = $app.DisplayName
        NovolisInnoAppVersion             = $PackageVersion
        NovolisInnoPublishDir             = $PublishDir
        NovolisInnoAppExeName             = $app.ExeName
        NovolisInnoOutputDir              = $InstallerDir
        NovolisInnoAppId                  = $app.AppId
        NovolisInnoDefaultGroupName       = $app.GroupName
        NovolisInnoOutputBaseFilename     = $setupBase
        NovolisInnoInstallDirName         = $app.InstallDir
        NovolisInnoScriptPath             = $script
        NovolisInnoAppPublisher           = 'Novolis'
        NovolisInnoAppPublisherURL        = 'https://github.com/Novolis-Platform'
        NovolisInnoAppCopyright           = 'Copyright (c) Novolis'
        NovolisInnoVersionInfoCompany     = 'Novolis'
        NovolisInnoVersionInfoDescription = "$($app.DisplayName) - Novolis"
        NovolisInnoAppSupportURL          = 'https://github.com/Novolis-Platform/novolis-apps/issues'
        NovolisInnoAppUpdatesURL          = 'https://github.com/Novolis-Platform/novolis-apps/releases'
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
