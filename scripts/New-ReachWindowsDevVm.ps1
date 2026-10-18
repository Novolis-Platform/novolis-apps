#Requires -Version 7.0
#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Creates a Generation 2 Windows evaluation VM for Reach host testing.

.DESCRIPTION
  This script prepares the Hyper-V side only. Windows installation remains
  interactive so no account credentials or product secrets are stored in the
  repository. Use a current Windows 11 Enterprise Evaluation ISO obtained from
  Microsoft Evaluation Center.

  Reach clients on the main Windows system and on a physical Android device
  need the guest to be on the physical LAN. Pass -CreateExternalSwitch on the
  first run, or pass the name of an existing external switch.
#>
[CmdletBinding()]
param(
    [string]$WindowsIso = '',

    [string]$VmName = 'ReachWindowsDev',

    [string]$VmRoot = 'D:\HyperV\ReachWindowsDev',

    [string]$SwitchName = '',

    [string]$ExternalAdapterName = '',

    [switch]$CreateExternalSwitch,

    [ValidateRange(4, 32)]
    [int]$MemoryStartupGb = 12,

    [ValidateRange(2, 32)]
    [int]$MemoryMinimumGb = 8,

    [ValidateRange(4, 64)]
    [int]$MemoryMaximumGb = 24,

    [ValidateRange(2, 24)]
    [int]$CpuCount = 6,

    [ValidateRange(64, 2048)]
    [int]$DiskSizeGb = 128,

    [switch]$ReuseExisting,

    [switch]$NoStart
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RequiredPath {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Windows ISO was not found: $Path"
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

function Get-OrCreateExternalSwitch {
    param(
        [Parameter(Mandatory)][string]$RequestedName,
        [Parameter(Mandatory)][string]$RequestedAdapter,
        [Parameter(Mandatory)][bool]$AllowCreate
    )

    $existingExternal = Get-VMSwitch -ErrorAction Stop |
        Where-Object SwitchType -eq 'External' |
        Select-Object -First 1
    if ($existingExternal -and [string]::IsNullOrWhiteSpace($RequestedName)) {
        return $existingExternal
    }

    if (-not [string]::IsNullOrWhiteSpace($RequestedName)) {
        $namedSwitch = Get-VMSwitch -Name $RequestedName -ErrorAction SilentlyContinue
        if ($namedSwitch) {
            if ($namedSwitch.SwitchType -ne 'External') {
                throw "Hyper-V switch '$RequestedName' is $($namedSwitch.SwitchType), not External."
            }

            return $namedSwitch
        }
    }

    if (-not $AllowCreate) {
        throw @"
No external Hyper-V switch is available. A Default Switch is not sufficient for
the physical Android client because its NAT network does not put the guest on
the same LAN. Re-run elevated with:

pwsh -File $PSScriptRoot\New-ReachWindowsDevVm.ps1 -WindowsIso "$WindowsIso" -CreateExternalSwitch -ExternalAdapterName Ethernet
"@
    }

    $adapter = if (-not [string]::IsNullOrWhiteSpace($RequestedAdapter)) {
        Get-NetAdapter -Name $RequestedAdapter -ErrorAction Stop
    }
    else {
        Get-NetAdapter -Physical -ErrorAction Stop |
            Where-Object Status -eq 'Up' |
            Sort-Object ifIndex |
            Select-Object -First 1
    }
    if (-not $adapter) {
        throw 'No active physical network adapter was found for the external switch.'
    }

    $newName = if ([string]::IsNullOrWhiteSpace($RequestedName)) {
        'Reach External'
    }
    else {
        $RequestedName
    }
    Write-Host "Creating external Hyper-V switch '$newName' on '$($adapter.Name)'."
    return New-VMSwitch `
        -Name $newName `
        -NetAdapterName $adapter.Name `
        -AllowManagementOS $true
}

if (-not (Get-Command New-VM -ErrorAction SilentlyContinue)) {
    throw 'Hyper-V PowerShell cmdlets are unavailable. Enable Hyper-V and reopen an elevated PowerShell.'
}

if ($MemoryMinimumGb -gt $MemoryStartupGb) {
    throw '-MemoryMinimumGb cannot exceed -MemoryStartupGb.'
}

if ($MemoryStartupGb -gt $MemoryMaximumGb) {
    throw '-MemoryStartupGb cannot exceed -MemoryMaximumGb.'
}

$existing = Get-VM -Name $VmName -ErrorAction SilentlyContinue
if ($existing -and -not $ReuseExisting) {
    throw "VM '$VmName' already exists. Use -ReuseExisting to configure it without deleting its disk."
}

$isoPath = $null
$iso = $null
if (-not [string]::IsNullOrWhiteSpace($WindowsIso)) {
    $isoPath = Get-RequiredPath -Path $WindowsIso
    $iso = Get-Item -LiteralPath $isoPath
    if ($iso.Length -lt 1GB) {
        throw "The file does not look like a Windows installation ISO: $isoPath"
    }
}
elseif (-not $existing) {
    throw 'WindowsIso is required when creating a new VM. Use -ReuseExisting to configure an installed VM without an ISO.'
}

$vmRootPath = [IO.Path]::GetFullPath($VmRoot)
New-Item -ItemType Directory -Path $vmRootPath -Force | Out-Null
$vhdxPath = Join-Path $vmRootPath "$VmName.vhdx"

$switch = Get-OrCreateExternalSwitch `
    -RequestedName $SwitchName `
    -RequestedAdapter $ExternalAdapterName `
    -AllowCreate $CreateExternalSwitch.IsPresent

if (-not $existing) {
    if (Test-Path -LiteralPath $vhdxPath) {
        throw "A VHDX already exists at $vhdxPath but no VM named '$VmName' exists. Choose another -VmName or inspect the disk before attaching it."
    }

    $freeBytes = (Get-PSDrive -Name ([IO.Path]::GetPathRoot($vmRootPath).TrimEnd('\')[0]) -PSProvider FileSystem).Free
    $requiredBytes = [uint64]($DiskSizeGb + 8) * 1GB
    if ($freeBytes -lt $requiredBytes) {
        throw "The volume containing $vmRootPath has less than $($DiskSizeGb + 8) GB free."
    }

    Write-Host "Creating $VmName on switch '$($switch.Name)'."
    New-VHD -Path $vhdxPath -SizeBytes ([uint64]$DiskSizeGb * 1GB) -Dynamic | Out-Null
    $vm = New-VM `
        -Name $VmName `
        -Generation 2 `
        -Path $vmRootPath `
        -MemoryStartupBytes ([uint64]$MemoryStartupGb * 1GB) `
        -VHDPath $vhdxPath `
        -SwitchName $switch.Name
}
else {
    $vm = $existing
    if ($vm.State -ne 'Off') {
        throw "VM '$VmName' is $($vm.State). Stop it before applying VM configuration."
    }

    $currentSwitch = Get-VMNetworkAdapter -VMName $VmName |
        Select-Object -First 1 -ExpandProperty SwitchName
    if ($currentSwitch -ne $switch.Name) {
        Connect-VMNetworkAdapter -VMName $VmName -SwitchName $switch.Name
    }
}

Set-VMMemory `
    -VMName $VmName `
    -DynamicMemoryEnabled $true `
    -StartupBytes ([uint64]$MemoryStartupGb * 1GB) `
    -MinimumBytes ([uint64]$MemoryMinimumGb * 1GB) `
    -MaximumBytes ([uint64]$MemoryMaximumGb * 1GB)
Set-VMProcessor -VMName $VmName -Count $CpuCount
Set-VMFirmware `
    -VMName $VmName `
    -EnableSecureBoot On `
    -SecureBootTemplate MicrosoftWindows

$dvd = Get-VMDvdDrive -VMName $VmName -ErrorAction SilentlyContinue |
    Select-Object -First 1
if ($isoPath) {
    if (-not $dvd) {
        $dvd = Add-VMDvdDrive -VMName $VmName -Path $isoPath -Passthru
    }
    elseif ($dvd.Path -ne $isoPath) {
        Set-VMDvdDrive -VMName $VmName -Path $isoPath
        $dvd = Get-VMDvdDrive -VMName $VmName | Select-Object -First 1
    }
    Set-VMFirmware -VMName $VmName -FirstBootDevice $dvd
}

$guestService = Get-VMIntegrationService `
    -VMName $VmName `
    -Name 'Guest Service Interface' `
    -ErrorAction SilentlyContinue
if ($guestService -and -not $guestService.Enabled) {
    Enable-VMIntegrationService -VMName $VmName -Name 'Guest Service Interface'
}

Set-VM `
    -VMName $VmName `
    -AutomaticStartAction StartIfRunning `
    -AutomaticStopAction ShutDown `
    -Notes 'Novolis Reach Windows host test VM. Use an active logged-in Windows session for screen capture.'

if (-not $NoStart) {
    Start-VM -Name $VmName | Out-Null
}

[pscustomobject]@{
    VmName = $VmName
    State = (Get-VM -Name $VmName).State
    Switch = $switch.Name
    Vhdx = $vhdxPath
    WindowsIso = $isoPath
    MemoryStartupGb = $MemoryStartupGb
    MemoryMinimumGb = $MemoryMinimumGb
    MemoryMaximumGb = $MemoryMaximumGb
    CpuCount = $CpuCount
    SecureBoot = (Get-VMFirmware -VMName $VmName).SecureBoot
    NextStep = "Open VMConnect for '$VmName', install Windows, sign in to an interactive user session, then run Install-ReachWindowsHost.ps1 inside the guest."
} | Format-List
