#Requires -Version 7.0
# Shared publish + zip + Inno script generation for novolis-apps WinExe projects.

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
    dotnet restore $appProject -r win-x64 @cfgArgs @versionArgs
    if ($LASTEXITCODE -ne 0) { throw "Restore failed with exit code $LASTEXITCODE." }

    dotnet publish $appProject `
        -c Release `
        -r win-x64 `
        --self-contained true `
        --no-restore `
        -o $publishDir `
        @versionArgs
    if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE." }

    $exeBase = [System.IO.Path]::GetFileNameWithoutExtension($appProject)
    $zipName = "$exeBase-$PackageVersion-win-x64.zip"
    $zipPath = Join-Path $stagingDir $zipName
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath
    Write-Host "Portable zip: $zipPath"

    $result = [ordered]@{
        AppKey       = $AppKey
        ZipPath      = $zipPath
        ZipName      = $zipName
        InstallerPath = $null
        InstallerName = $null
    }

    if ($SkipInstaller) {
        return [pscustomobject]$result
    }

    $inno = Get-NovolisAppInnoProfile -AppKey $AppKey -PackageVersion $PackageVersion -PublishDir $publishDir -InstallerDir $installerDir
    dotnet msbuild $appProject `
        -t:NovolisGenerateInnoScript `
        @($inno.MsBuildArgs.GetEnumerator() | ForEach-Object { "-p:$($_.Key)=$($_.Value)" })
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

    & $iscc $inno.ScriptPath
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
        [Parameter(Mandatory)][string]$InstallerDir
    )

    switch ($AppKey) {
        'manuscript-studio' {
            $exe = 'ManuscriptStudio.exe'
            $script = Join-Path $InstallerDir 'manuscript-studio.iss'
            return [pscustomobject]@{
                ScriptPath    = $script
                InstallerPath = Join-Path $InstallerDir "ManuscriptStudioSetup-$PackageVersion-win-x64.exe"
                MsBuildArgs   = @{
                    NovolisInnoAppName              = 'Manuscript Studio'
                    NovolisInnoAppVersion           = $PackageVersion
                    NovolisInnoPublishDir           = $PublishDir
                    NovolisInnoAppExeName           = $exe
                    NovolisInnoOutputDir            = $InstallerDir
                    NovolisInnoAppId                = 'Novolis.ManuscriptStudio'
                    NovolisInnoDefaultGroupName     = 'Manuscript Studio'
                    NovolisInnoOutputBaseFilename   = "ManuscriptStudioSetup-$PackageVersion-win-x64"
                    NovolisInnoInstallDirName       = 'Novolis\Manuscript Studio'
                    NovolisInnoScriptPath           = $script
                    NovolisInnoAppSupportURL        = 'https://github.com/Novolis-Platform/novolis-apps/issues'
                    NovolisInnoAppUpdatesURL        = 'https://github.com/Novolis-Platform/novolis-apps/releases'
                }
            }
        }
        'concept-studio' {
            $exe = 'ConceptStudio.exe'
            $script = Join-Path $InstallerDir 'concept-studio.iss'
            return [pscustomobject]@{
                ScriptPath    = $script
                InstallerPath = Join-Path $InstallerDir "ConceptStudioSetup-$PackageVersion-win-x64.exe"
                MsBuildArgs   = @{
                    NovolisInnoAppName              = 'Concept Studio'
                    NovolisInnoAppVersion           = $PackageVersion
                    NovolisInnoPublishDir           = $PublishDir
                    NovolisInnoAppExeName           = $exe
                    NovolisInnoOutputDir            = $InstallerDir
                    NovolisInnoAppId                = 'Novolis.ConceptStudio'
                    NovolisInnoDefaultGroupName     = 'Concept Studio'
                    NovolisInnoOutputBaseFilename   = "ConceptStudioSetup-$PackageVersion-win-x64"
                    NovolisInnoInstallDirName       = 'Novolis\Concept Studio'
                    NovolisInnoScriptPath           = $script
                    NovolisInnoAppSupportURL        = 'https://github.com/Novolis-Platform/novolis-apps/issues'
                    NovolisInnoAppUpdatesURL        = 'https://github.com/Novolis-Platform/novolis-apps/releases'
                }
            }
        }
        default { throw "Unknown app key: $AppKey" }
    }
}
