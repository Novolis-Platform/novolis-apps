#Requires -Version 7.0
<#
.SYNOPSIS
  Builds Reach clients, checks the Windows guest endpoint, and optionally runs
  the Windows and Android Appium timeline.

.DESCRIPTION
  Run this on the main Windows system after the guest host is installed. The
  script performs the network check before starting UI evidence so a bad
  Hyper-V switch or firewall cannot look like an application failure.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Endpoint,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [string]$WindowsClientOutput = '',

    [string]$AndroidApkOutput = '',

    [string]$AppiumHost = 'http://127.0.0.1:4723/',

    [string]$AndroidSerial = '',

    [switch]$SkipBuild,

    [switch]$SkipAndroidBuild,

    [switch]$InstallAndroid,

    [switch]$RunUiTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($WindowsClientOutput)) {
    $WindowsClientOutput = Join-Path `
        $repoRoot `
        "artifacts\publish\Reach.Client.Windows\$($Configuration.ToLowerInvariant())"
}
if ([string]::IsNullOrWhiteSpace($AndroidApkOutput)) {
    $AndroidApkOutput = Join-Path `
        $repoRoot `
        "artifacts\publish\Reach.Client.Android\$($Configuration.ToLowerInvariant())"
}

function Get-EndpointUri {
    param([Parameter(Mandatory)][string]$Value)

    $uri = $null
    if (-not [Uri]::TryCreate($Value, [UriKind]::Absolute, [ref]$uri)) {
        throw "Reach endpoint is not an absolute URI: $Value"
    }
    if ($uri.Scheme -notin @('tcp', 'quic')) {
        throw "Reach endpoint must use tcp:// or quic://: $Value"
    }
    if ($uri.Port -le 0) {
        throw "Reach endpoint does not contain a port: $Value"
    }

    return $uri
}

function Invoke-Dotnet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed with exit code $LASTEXITCODE."
    }
}

function Test-TcpPort {
    param(
        [Parameter(Mandatory)][string]$HostName,
        [Parameter(Mandatory)][int]$Port
    )

    $result = Test-NetConnection `
        -ComputerName $HostName `
        -Port $Port `
        -InformationLevel Quiet `
        -WarningAction SilentlyContinue
    if (-not $result) {
        throw "TCP $HostName`:$Port is not reachable. Check the external Hyper-V switch and the guest firewall."
    }
}

function Invoke-Adb {
    param([Parameter(Mandatory)][string]$AdbPath, [Parameter(Mandatory)][string[]]$Arguments)

    & $AdbPath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "adb failed with exit code $LASTEXITCODE."
    }
}

function Resolve-AndroidApk {
    param(
        [Parameter(Mandatory)][string]$RequestedOutput,
        [Parameter(Mandatory)][string]$Configuration
    )

    $requestedRoot = [IO.Path]::GetFullPath($RequestedOutput)
    $apk = Get-ChildItem `
        -LiteralPath $requestedRoot `
        -Filter '*.apk' `
        -File `
        -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if ($apk) {
        return $apk
    }

    # UseArtifactsOutput routes .NET Android packages to the canonical bin
    # directory even when dotnet publish receives a custom -o directory.
    $canonicalRoot = Join-Path `
        $repoRoot `
        "artifacts\bin\Reach.Client.Android\$($Configuration.ToLowerInvariant())"
    $apk = Get-ChildItem `
        -LiteralPath $canonicalRoot `
        -Filter '*-Signed.apk' `
        -File `
        -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if (-not $apk) {
        throw "Reach Android APK was not found under $requestedRoot or $canonicalRoot."
    }

    New-Item -ItemType Directory -Path $requestedRoot -Force | Out-Null
    $copiedPath = Join-Path $requestedRoot $apk.Name
    Copy-Item -LiteralPath $apk.FullName -Destination $copiedPath -Force
    return Get-Item -LiteralPath $copiedPath
}

$endpointUri = Get-EndpointUri -Value $Endpoint
Test-TcpPort -HostName $endpointUri.Host -Port $endpointUri.Port
if ($endpointUri.Port -eq 19800) {
    Test-TcpPort -HostName $endpointUri.Host -Port 19801
}

$windowsProject = Join-Path `
    $repoRoot `
    'src\Reach\Reach.Client.Windows\Reach.Client.Windows.csproj'
$androidProject = Join-Path `
    $repoRoot `
    'src\Reach\Reach.Client.Android\Reach.Client.Android.csproj'
$uiTestProject = Join-Path `
    $repoRoot `
    'tests\Reach.UiTests\Reach.UiTests.csproj'

if (-not $SkipBuild) {
    New-Item -ItemType Directory -Path $WindowsClientOutput -Force | Out-Null
    Invoke-Dotnet -Arguments @(
        'publish',
        $windowsProject,
        '-c', $Configuration,
        '-r', 'win-x64',
        '--self-contained', 'true',
        '-p:NovolisUseProjectReferences=false',
        '-p:PublishSingleFile=false',
        '-p:PublishTrimmed=false',
        '-o', $WindowsClientOutput
    )
}

$windowsExecutable = Join-Path `
    ([IO.Path]::GetFullPath($WindowsClientOutput)) `
    'Novolis.Reach.Client.Windows.exe'
if (-not (Test-Path -LiteralPath $windowsExecutable -PathType Leaf)) {
    throw "Reach Windows client executable was not found: $windowsExecutable"
}

$androidApk = $null
if (-not $SkipAndroidBuild) {
    New-Item -ItemType Directory -Path $AndroidApkOutput -Force | Out-Null
    Invoke-Dotnet -Arguments @(
        'publish',
        $androidProject,
        '-f', 'net10.0-android',
        '-c', $Configuration,
        '-p:AndroidPackageFormat=apk',
        '-p:AndroidPackageFormats=apk',
        '-p:AndroidBuildApplicationPackage=true',
        '-p:AndroidFastDeployment=false',
        '-p:EmbedAssembliesIntoApk=true',
        '-p:NovolisUseProjectReferences=false',
        '-o', $AndroidApkOutput
    )
}

$androidApk = Resolve-AndroidApk `
    -RequestedOutput $AndroidApkOutput `
    -Configuration $Configuration

$androidInstalled = $false
if ($InstallAndroid) {
    $androidHome = $env:ANDROID_HOME
    if ([string]::IsNullOrWhiteSpace($androidHome)) {
        $androidHome = Join-Path $env:LOCALAPPDATA 'Android\Sdk'
    }
    $adbPath = Join-Path $androidHome 'platform-tools\adb.exe'
    if (-not (Test-Path -LiteralPath $adbPath -PathType Leaf)) {
        throw "adb was not found at $adbPath. Set ANDROID_HOME to the Android SDK root."
    }

    $adbPrefix = @()
    if (-not [string]::IsNullOrWhiteSpace($AndroidSerial)) {
        $adbPrefix = @('-s', $AndroidSerial)
    }
    Invoke-Adb `
        -AdbPath $adbPath `
        -Arguments ($adbPrefix + @('install', '-r', $androidApk.FullName))
    Invoke-Adb `
        -AdbPath $adbPath `
        -Arguments ($adbPrefix + @('shell', 'monkey', '-p', 'com.novolis.reach', '1'))
    $androidInstalled = $true
}

if ($RunUiTests) {
    $appiumUri = $null
    if (-not [Uri]::TryCreate(
            $AppiumHost,
            [UriKind]::Absolute,
            [ref]$appiumUri) -or
        $appiumUri.Scheme -notin @('http', 'https') -or
        $appiumUri.Port -le 0) {
        throw "Appium host is not a valid HTTP endpoint: $AppiumHost"
    }
    Test-TcpPort -HostName $appiumUri.Host -Port $appiumUri.Port

    $env:APPIUM_HOST = $AppiumHost
    $env:NOVOLIS_REACH_UI_APP = $windowsExecutable
    $env:NOVOLIS_REACH_UI_ENDPOINT = $Endpoint
    $env:NOVOLIS_REACH_UI_ANDROID_ENDPOINT = $Endpoint
    $env:NOVOLIS_REACH_UI_ANDROID_APK = $androidApk.FullName
    if (-not [string]::IsNullOrWhiteSpace($AndroidSerial)) {
        $env:ANDROID_SERIAL = $AndroidSerial
    }

    Invoke-Dotnet -Arguments @(
        'test',
        $uiTestProject,
        '-p:NovolisUseProjectReferences=true'
    )
}

[pscustomobject]@{
    Endpoint = $Endpoint
    WindowsClient = $windowsExecutable
    AndroidApk = $androidApk.FullName
    AndroidInstalled = $androidInstalled
    AppiumHost = if ($RunUiTests) { $AppiumHost } else { 'not-run' }
    UiTests = $RunUiTests.IsPresent
} | Format-List

Write-Host ''
Write-Host 'Manual Windows client smoke: launch the Windows client above and enter the same endpoint.'
Write-Host 'Android smoke: install the APK on the selected device and enter the same endpoint.'
