#Requires -Version 7.0
<#
.SYNOPSIS
  Shared Android SDK and novolis-android readiness checks.
#>

function Initialize-NovolisAndroidDeployment {
    [CmdletBinding()]
    param(
        [string]$Serial = '',
        [string]$AndroidTool = 'novolis-android'
    )

    if ([string]::IsNullOrWhiteSpace($env:ANDROID_HOME)) {
        $defaultSdk = Join-Path $env:LOCALAPPDATA 'Android\Sdk'
        if (Test-Path $defaultSdk) {
            $env:ANDROID_HOME = $defaultSdk
            Write-Host "ANDROID_HOME=$env:ANDROID_HOME"
        } else {
            throw "ANDROID_HOME is not set and $defaultSdk was not found."
        }
    }

    $adb = Join-Path $env:ANDROID_HOME 'platform-tools\adb.exe'
    if (-not (Test-Path $adb)) {
        throw "adb not found at $adb"
    }

    $tool = Get-Command $AndroidTool -ErrorAction SilentlyContinue
    if ($null -eq $tool) {
        throw "$AndroidTool was not found. Install Novolis.Tools.Android.Cli or pass -AndroidTool with an installed command."
    }

    if (-not [string]::IsNullOrWhiteSpace($Serial)) {
        $env:ANDROID_SERIAL = $Serial
    }

    $toolArgs = @('--no-color', 'info')
    if (-not [string]::IsNullOrWhiteSpace($Serial)) {
        $toolArgs += @('--serial', $Serial)
    }

    & $AndroidTool @toolArgs
    if ($LASTEXITCODE -ne 0) {
        throw "$AndroidTool could not select a ready Android device."
    }

    return $adb
}
