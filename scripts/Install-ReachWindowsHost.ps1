#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Installs and starts the published Reach Windows host.

.DESCRIPTION
  Run this script in the Windows guest after copying the self-contained
  Reach host bundle to Windows. It installs the machine-wide service and
  private-network firewall rules.
  It also installs the limited per-user management task. Capture stays idle
  until a remote client opens or resumes a session.
#>
[CmdletBinding()]
param(
    [string]$ApplicationRoot = $PSScriptRoot,

    [string[]]$RemoteAddress = @('LocalSubnet'),

    [string]$ManagementUser = '',

    [string]$ManagementTaskName = 'Novolis Reach Management',

    [switch]$SkipServiceInstall,

    [switch]$SkipManagementTask,

    [switch]$Uninstall
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
            Remove-NetFirewallRule `
                -DisplayName $ruleName `
                -ErrorAction Stop
        }

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

$root = [IO.Path]::GetFullPath($ApplicationRoot)
$serviceExecutable = Join-Path $root 'Novolis.Reach.Host.Windows.Service.exe'
$sessionExecutable = Join-Path $root 'Novolis.Reach.Host.Windows.exe'
$serviceName = 'Novolis Reach Host'
$firewallRuleNames = @(
    'Novolis Reach TCP',
    'Novolis Reach TCP 19801',
    'Novolis Reach TCP 19803',
    'Novolis Reach UDP',
    'Novolis Reach UDP 19801',
    'Novolis Reach UDP 19802'
)

function Invoke-Sc {
    param(
        [Parameter(Mandatory)][string[]]$Arguments
    )

    & sc.exe @Arguments | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "sc.exe failed with exit code $LASTEXITCODE`: $($Arguments -join ' ')"
    }
}

function Resolve-ManagementUser {
    if ([string]::IsNullOrWhiteSpace($ManagementUser)) {
        # Prefer the active console user so an elevated install performed with
        # a different administrator account still installs the tray task for
        # the person who is using the guest.
        $computer = Get-CimInstance -ClassName Win32_ComputerSystem
        if (-not [string]::IsNullOrWhiteSpace($computer.UserName)) {
            return [string]$computer.UserName
        }

        $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
        if ($identity -notlike 'NT AUTHORITY\*') {
            return $identity
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($ManagementUser)) {
        return $ManagementUser
    }

    throw 'No logged-in Windows user was found. Supply -ManagementUser when installing the management task.'
}

function Remove-ReachInstallation {
    $task = Get-ScheduledTask `
        -TaskName $ManagementTaskName `
        -ErrorAction SilentlyContinue
    if ($task) {
        Stop-ScheduledTask `
            -TaskName $ManagementTaskName `
            -ErrorAction SilentlyContinue
        Unregister-ScheduledTask `
            -TaskName $ManagementTaskName `
            -Confirm:$false
    }

    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($service) {
        if ($service.Status -ne 'Stopped') {
            Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
        }
        Invoke-Sc -Arguments @('delete', $serviceName)
    }

    foreach ($ruleName in $firewallRuleNames) {
        Get-NetFirewallRule `
            -DisplayName $ruleName `
            -ErrorAction SilentlyContinue |
            Remove-NetFirewallRule `
                -ErrorAction SilentlyContinue
    }
}

if ($Uninstall) {
    Remove-ReachInstallation
    Write-Host 'Novolis Reach service, management task, and firewall rules removed.'
    return
}

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

if (-not $SkipServiceInstall) {
    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if (-not $service) {
        New-Service `
            -Name $serviceName `
            -BinaryPathName "`"$serviceExecutable`"" `
            -DisplayName $serviceName `
            -Description 'Novolis Reach machine-wide host service.' `
            -StartupType Automatic | Out-Null
    }
    else {
        Invoke-Sc -Arguments @(
            'config',
            $serviceName,
            'binPath=',
            "`"$serviceExecutable`""
        )
    }

    Invoke-Sc -Arguments @(
        'config',
        $serviceName,
        'obj=',
        'LocalSystem'
    )
    Set-Service -Name $serviceName -StartupType Automatic
    Invoke-Sc -Arguments @(
        'config',
        $serviceName,
        'start=',
        'delayed-auto'
    )
    Invoke-Sc -Arguments @(
        'failure',
        $serviceName,
        'reset=',
        '86400',
        'actions=',
        'restart/5000/restart/30000/restart/60000'
    )
    Invoke-Sc -Arguments @('failureflag', $serviceName, '1')

    $service = Get-Service -Name $serviceName
    if ($service.Status -eq 'Running') {
        Restart-Service -Name $serviceName -Force
    }
    else {
        Start-Service -Name $serviceName
    }

    $service = Get-Service -Name $serviceName
    $service.WaitForStatus(
        [System.ServiceProcess.ServiceControllerStatus]::Running,
        [TimeSpan]::FromSeconds(30))
}

$managementUser = $null
if (-not $SkipManagementTask) {
    $managementUser = Resolve-ManagementUser
    $existingTask = Get-ScheduledTask `
        -TaskName $ManagementTaskName `
        -ErrorAction SilentlyContinue
    if ($existingTask) {
        Unregister-ScheduledTask `
            -TaskName $ManagementTaskName `
            -Confirm:$false
    }

    $action = New-ScheduledTaskAction `
        -Execute $sessionExecutable `
        -Argument '--tray' `
        -WorkingDirectory $root
    $trigger = New-ScheduledTaskTrigger `
        -AtLogOn `
        -User $managementUser
    $principal = New-ScheduledTaskPrincipal `
        -UserId $managementUser `
        -LogonType Interactive `
        -RunLevel Limited
    $settings = New-ScheduledTaskSettingsSet `
        -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries
    Register-ScheduledTask `
        -TaskName $ManagementTaskName `
        -Action $action `
        -Trigger $trigger `
        -Principal $principal `
        -Settings $settings `
        -Description 'Novolis Reach per-user management tray and diagnostics.' `
        -Force | Out-Null

    try {
        Start-ScheduledTask -TaskName $ManagementTaskName
    }
    catch {
        Write-Warning "The management task was installed but could not start in the current session: $($_.Exception.Message)"
    }
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
    ManagementTaskName = if ($SkipManagementTask) { 'not-installed-by-script' } else { $ManagementTaskName }
    ManagementUser = $managementUser
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
