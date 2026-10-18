#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Installs and starts the published Reach Windows host inside the test VM.

.DESCRIPTION
  Run this script in the Windows guest after copying the self-contained
  Reach host bundle to the guest. It configures only the Reach listener ports,
  installs the headless host as an automatic Windows service, and prints the
  endpoint that the Windows and Android clients should use.
#>
[CmdletBinding()]
param(
    [string]$ApplicationRoot = $PSScriptRoot,

    [string[]]$RemoteAddress = @('LocalSubnet'),

    [switch]$SkipServiceInstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Add-OrUpdateFirewallRule {
    param(
        [Parameter(Mandatory)][string]$DisplayName,
        [Parameter(Mandatory)][string]$Protocol,
        [Parameter(Mandatory)][string[]]$LocalPort
    )

    $ports = @($LocalPort | ForEach-Object { [string]$_ })
    for ($index = 0; $index -lt $ports.Count; $index++) {
        $port = $ports[$index]
        $ruleName = if ($index -eq 0) {
            $DisplayName
        }
        else {
            "$DisplayName $port"
        }

        $rule = Get-NetFirewallRule `
            -DisplayName $ruleName `
            -ErrorAction SilentlyContinue
        if ($rule) {
            Set-NetFirewallRule `
                -DisplayName $ruleName `
                -Enabled True `
                -Action Allow `
                -Profile Any `
                -Protocol $Protocol `
                -LocalPort $port `
                -RemoteAddress $RemoteAddress | Out-Null
        }
        else {
            New-NetFirewallRule `
                -DisplayName $ruleName `
                -Direction Inbound `
                -Action Allow `
                -Enabled True `
                -Profile Any `
                -Protocol $Protocol `
                -LocalPort $port `
                -RemoteAddress $RemoteAddress | Out-Null
        }
    }
}

$root = [IO.Path]::GetFullPath($ApplicationRoot)
$serviceExecutable = Join-Path $root 'Novolis.Reach.Host.Windows.Service.exe'
$sessionExecutable = Join-Path $root 'Novolis.Reach.Host.Windows.exe'
if (-not (Test-Path -LiteralPath $serviceExecutable -PathType Leaf)) {
    throw "Reach service executable was not found: $serviceExecutable"
}
if (-not (Test-Path -LiteralPath $sessionExecutable -PathType Leaf)) {
    throw "Reach interactive host executable was not found beside the service: $sessionExecutable"
}

Add-OrUpdateFirewallRule `
    -DisplayName 'Novolis Reach TCP' `
    -Protocol TCP `
    -LocalPort @('19800', '19801', '19803')
Add-OrUpdateFirewallRule `
    -DisplayName 'Novolis Reach UDP' `
    -Protocol UDP `
    -LocalPort @('19800', '19801', '19802')

$serviceName = 'Novolis Reach Host'
if (-not $SkipServiceInstall) {
    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if (-not $service) {
        New-Service `
            -Name $serviceName `
            -BinaryPathName "`"$serviceExecutable`"" `
            -DisplayName $serviceName `
            -Description 'Novolis Reach host for the dedicated Windows test VM.' `
            -StartupType Automatic | Out-Null
    }
    elseif ($service.Status -eq 'Running') {
        Stop-Service -Name $serviceName -Force
    }

    Set-Service -Name $serviceName -StartupType Automatic
    Start-Service -Name $serviceName
    $service = Get-Service -Name $serviceName
    $service.WaitForStatus(
        [System.ServiceProcess.ServiceControllerStatus]::Running,
        [TimeSpan]::FromSeconds(30))
}

$addresses = Get-NetIPAddress -AddressFamily IPv4 |
    Where-Object {
        $_.IPAddress -notlike '127.*' -and
        $_.IPAddress -notlike '169.254.*'
    } |
    Sort-Object InterfaceIndex, IPAddress

$tcpListeners = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue |
    Where-Object LocalPort -in @(19800, 19801, 19803)
$udpListeners = Get-NetUDPEndpoint -ErrorAction SilentlyContinue |
    Where-Object LocalPort -in @(19800, 19801, 19802)

[pscustomobject]@{
    ApplicationRoot = $root
    ServiceName = $serviceName
    ServiceStatus = if ($SkipServiceInstall) { 'not-started-by-script' } else { (Get-Service -Name $serviceName).Status }
    IPv4Addresses = @($addresses | ForEach-Object IPAddress)
    TcpListeners = @($tcpListeners | ForEach-Object { "$($_.LocalAddress):$($_.LocalPort)" })
    UdpListeners = @($udpListeners | ForEach-Object { "$($_.LocalAddress):$($_.LocalPort)" })
    ClientEndpoints = @(
        $addresses | ForEach-Object { "tcp://$($_.IPAddress):19800" }
    )
    FirewallRemoteAddress = $RemoteAddress -join ','
} | Format-List

Write-Host ''
Write-Host 'Use one tcp:// endpoint above in the Windows and Android clients.'
Write-Host 'The HTTPS SignalR side channel uses TCP port 19803 when available.'
Write-Host 'The guest must keep an active logged-in Windows session for screen capture.'
