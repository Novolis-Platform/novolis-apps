#Requires -Version 7.0
# Smoke: generate an Inno script for one app (MSBuild target or sibling generator fallback).
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$AppKey = 'draft-studio'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Publish-NovolisApp.ps1')

$catalog = Get-NovolisAppCatalog -RepoRoot $RepoRoot | Where-Object Key -eq $AppKey | Select-Object -First 1
if (-not $catalog) { throw "Unknown app key $AppKey" }

$staging = Join-Path $RepoRoot "artifacts/$AppKey/installer-smoke"
$publishDir = Join-Path $staging 'app'
$installerDir = Join-Path $staging 'installer'
New-Item -ItemType Directory -Force -Path $publishDir, $installerDir | Out-Null
Set-Content -Path (Join-Path $publishDir $catalog.ExeName) -Value 'placeholder'

$inno = Get-NovolisAppInnoProfile -AppKey $AppKey -PackageVersion '2026.1.0.1' -PublishDir $publishDir -InstallerDir $installerDir -RepoRoot $RepoRoot
$project = Join-Path $RepoRoot $catalog.Project
$cfgArgs = @()
$nugetConfig = Join-Path $RepoRoot 'nuget.config'
if (Test-Path $nugetConfig) { $cfgArgs = @('--configfile', $nugetConfig) }

$generated = $false
& dotnet restore $project @cfgArgs | Out-Null
if ($LASTEXITCODE -eq 0) {
    $msbuildProps = @(Get-NovolisMsBuildPropertyArgs -Properties ([hashtable]$inno.MsBuildArgs))
    & dotnet msbuild $project -t:NovolisGenerateInnoScript @msbuildProps | Out-Null
    if ($LASTEXITCODE -eq 0 -and (Test-Path -LiteralPath $inno.ScriptPath)) {
        $generated = $true
    }
}

if (-not $generated) {
    # Fallback: sibling Packaging.Inno generator (ProjectRef / pre-publish).
    $packProj = Join-Path (Split-Path $RepoRoot -Parent) 'novolis-avalonia\src\Novolis.Avalonia.Packaging.Inno\Novolis.Avalonia.Packaging.Inno.csproj'
    if (-not (Test-Path -LiteralPath $packProj)) {
        throw "NovolisGenerateInnoScript unavailable and sibling Packaging.Inno not found at $packProj"
    }
    $tmp = Join-Path $env:TEMP "novolis-inno-gen-$AppKey"
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $tmp | Out-Null
    $filter = $catalog.CloseApplicationsFilter
    $code = @"
using Novolis.Avalonia.Packaging.Inno;
var script = new InnoScriptGenerator
{
    AppName = @"$($catalog.DisplayName.Replace('"','\"'))",
    AppVersion = "2026.1.0.1",
    PublishDir = @"$($publishDir.Replace('"','\"'))",
    AppExeName = @"$($catalog.ExeName.Replace('"','\"'))",
    CloseApplicationsFilter = @"$($filter.Replace('"','\"'))",
    OutputDir = @"$($installerDir.Replace('"','\"'))",
    AppId = @"$($catalog.AppId.Replace('"','\"'))",
    DefaultGroupName = @"$($catalog.GroupName.Replace('"','\"'))",
    OutputBaseFilename = @"$($catalog.SetupBase)-2026.1.0.1-win-x64",
    InstallDirName = @"$($catalog.InstallDir.Replace('"','\"'))",
}.Generate();
System.IO.File.WriteAllText(@"$($inno.ScriptPath.Replace('"','\"'))", script);
System.Console.WriteLine("Wrote " + @"$($inno.ScriptPath.Replace('"','\"'))");
"@
    Set-Content -Path (Join-Path $tmp 'Program.cs') -Value $code
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$packProj" />
  </ItemGroup>
</Project>
"@ | Set-Content -Path (Join-Path $tmp 'gen.csproj')
    & dotnet run --project (Join-Path $tmp 'gen.csproj')
    if ($LASTEXITCODE -ne 0) { throw "Sibling Inno generator failed" }
}

$script = Get-Content -LiteralPath $inno.ScriptPath -Raw
foreach ($needle in @(
    'PrivilegesRequired=lowest',
    'UsePreviousAppDir=yes',
    'AllowDowngrade=no',
    "AppId=$($catalog.AppId)",
    '{localappdata}\Programs\',
    'Flags: unchecked',
    "CloseApplicationsFilter=$($catalog.CloseApplicationsFilter)"
)) {
    if ($script -notlike "*$needle*") {
        throw "Generated Inno script missing: $needle"
    }
}

if ($script -match 'Program Files' -or $script -match 'PrivilegesRequired=admin') {
    throw 'Generated Inno script must not target Program Files or require admin.'
}

Write-Host "Installer smoke OK: $($inno.ScriptPath)"
