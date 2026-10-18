# Novolis Reach

Novolis Reach is a general-purpose remote interactive desktop product for
trusted networks. It attaches to an existing logged-in Windows interactive
session and transports live video, input, clipboard, audio, and later files.

Reach is deliberately separate from
[CursorRemote](https://github.com/Novolis-Platform/novolis-lab/tree/main/labs/apps/CursorRemote). CursorRemote remains a
narrow Android controller for the Cursor window: it uses its own discovery,
HTTP, PNG, and input contract. Reach does not rename, migrate, or reuse that
implementation.

## Processes

| Process | Role |
| --- | --- |
| `Novolis.Reach.Host.Windows.Service` | Headless Windows service; accepts remote clients and owns lifecycle |
| `Novolis.Reach.Host.Windows` | Per-user Windows host app; captures the interactive session and owns the tray icon |
| `Novolis.Reach.Client.Windows` | Windows client |
| `Novolis.Reach.Client.Linux` | Linux client |
| `Novolis.Reach.Client.Android` | Android client |

The service binds to loopback, private IPv4, and Tailscale IPv4 addresses.
Reach has no Novolis account, relay, or public authentication flow in this
personal-use shape.

The host uses TCP `19800` for control/input, TCP `19801` for reliable encoded
media fallback, and UDP `19802` for discovery. When QUIC is available, the
service advertises a pinned `quic://` control candidate alongside the TCP
candidate. A successful QUIC session receives an authenticated AES-GCM UDP
media offer on `19801`; clients fall back to QUIC streams or TCP when UDP is
unavailable.

Product-private libraries live next to the executables in this folder
(`Reach.Protocol`, `Reach.Transport`, `Reach.Client`, `Reach.Host.Server`,
`Reach.Host.Windows.Session`, `Reach.Client.Avalonia`). They are not NuGet
packages. Generic fit, stream gating, datagram reassembly, and the video
surface live in `Novolis.Video.*`, `Novolis.Transports.Datagrams`, and
`Novolis.Avalonia.Video`.

## Run locally

```powershell
dotnet run --project d:\novolis\novolis-apps\src\Reach\Reach.Host.Windows.Service\Reach.Host.Windows.Service.csproj -p:NovolisUseProjectReferences=true
dotnet run --project d:\novolis\novolis-apps\src\Reach\Reach.Host.Windows\Reach.Host.Windows.csproj -p:NovolisUseProjectReferences=true
dotnet run --project d:\novolis\novolis-apps\src\Reach\Reach.Client.Windows\Reach.Client.Windows.csproj -p:NovolisUseProjectReferences=true
```

Windows, Linux, and Android clients are day-one Reach clients. The release
catalog produces a Windows installer, a self-contained Linux tarball, and an
Android APK. The Windows installer contains the service and the per-user host
app; the service launches the host app in the active Windows session (or
directly when the service is running in the user's session), where it captures
the real screen and shows the Reach tray icon.

To build a packaged APK and install it on a booted Android emulator:

```powershell
dotnet publish d:\novolis\novolis-apps\src\Reach\Reach.Client.Android\Reach.Client.Android.csproj -p:NovolisUseProjectReferences=true -f net10.0-android -c Release -p:AndroidPackageFormats=apk -p:AndroidBuildApplicationPackage=true
& "$env:ANDROID_HOME\platform-tools\adb.exe" install -r d:\novolis\novolis-apps\artifacts\publish\Reach.Client.Android\release\com.novolis.reach-Signed.apk
```

For an emulator that cannot route to the Hyper-V guest address, reverse the
two reliable TCP ports through ADB and enter `tcp://127.0.0.1:19800` in Reach:

```powershell
& "$env:ANDROID_HOME\platform-tools\adb.exe" -s emulator-5554 reverse tcp:19800 tcp:19800
& "$env:ANDROID_HOME\platform-tools\adb.exe" -s emulator-5554 reverse tcp:19801 tcp:19801
```

The emulator route uses reliable TCP media; UDP media still requires a
reachable private-network address.

Launch Reach, allow discovery to finish, and connect to the Windows host.

Reach's protocol is product-private. Generic framing, datagrams, discovery,
Tailscale binding, Windows capabilities, and video codecs live in their
respective Novolis libraries and do not contain Reach types.

## Hyper-V Windows host

For a repeatable host/client smoke environment, use a current Windows 11
Enterprise Evaluation ISO from the
[Microsoft Evaluation Center](https://www.microsoft.com/en-us/evalcenter/evaluate-windows-11-enterprise).
The ISO requires Microsoft registration and is time-limited; do not commit it
or embed an account password in the repository.

The Reach VM must use an **external** Hyper-V switch. The Hyper-V Default
Switch is suitable for initial guest internet access, but it does not put a
physical Android device on the same LAN as the guest.

Create the Gen2 VM from an elevated PowerShell:

```powershell
pwsh -File d:\novolis\novolis-apps\scripts\New-ReachWindowsDevVm.ps1 `
  -WindowsIso d:\downloads\Win11_Enterprise_Evaluation.iso `
  -CreateExternalSwitch `
  -ExternalAdapterName Ethernet
```

If an installed Gen2 Windows VM already exists, reuse its VHDX without
supplying an ISO:

```powershell
Stop-VM -Name 'Windows 11 dev environment'
pwsh -File d:\novolis\novolis-apps\scripts\New-ReachWindowsDevVm.ps1 `
  -VmName 'Windows 11 dev environment' `
  -ReuseExisting `
  -CreateExternalSwitch `
  -ExternalAdapterName Ethernet
```

Install Windows interactively in `ReachWindowsDev`, sign in to an active
user session, and copy the host bundle into the guest. Publish the bundle on
the main Windows system with:

```powershell
pwsh -File d:\novolis\novolis-apps\scripts\Publish-ReachWindowsHost.ps1
```

Run the following inside the guest from an elevated PowerShell:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File C:\Novolis\Reach\Install-ReachWindowsHost.ps1 `
  -ApplicationRoot C:\Novolis\Reach `
  -RemoteAddress LocalSubnet
```

The guest script opens TCP `19800` and `19801`, UDP `19800`, `19801`, and
`19802`, registers `Novolis Reach Host` as an automatic service, and prints
the guest's `tcp://` endpoints. Keep the guest user session active and
unlocked so screen capture and input can be exercised.

Check the guest endpoint and build both clients from the main Windows system:

```powershell
pwsh -File d:\novolis\novolis-apps\scripts\Invoke-ReachClientSmoke.ps1 `
  -Endpoint tcp://10.0.0.25:19800
```

Add `-InstallAndroid -AndroidSerial <serial>` to install and launch the APK
on a connected device or booted emulator through the Android SDK.

Add `-RunUiTests` only after a Windows Appium driver and an Android UiAutomator2
device are already available. The command sets
`NOVOLIS_REACH_UI_ENDPOINT` and `NOVOLIS_REACH_UI_ANDROID_ENDPOINT` to the
same guest endpoint, so the Android timeline cannot pass without reaching the
Windows host.

## Usability validation

The first usability gate is a Windows client controlling a Windows host on a
trusted LAN or Tailscale path. Keep TCP `19800`, TCP `19801`, and UDP `19802`
reachable between the two private-network peers. The gate covers video,
pointer and keyboard input, Unicode text, host audio, text clipboard
synchronization, monitor selection, reconnect behavior, and adaptive quality.

Run the deterministic protocol and adapter coverage with project references:

```powershell
dotnet test d:\novolis\novolis-apps\tests\Reach.Unit\Reach.Unit.csproj -p:NovolisUseProjectReferences=true
dotnet test d:\novolis\novolis-apps\tests\Reach.Avalonia.Unit\Reach.Avalonia.Unit.csproj -p:NovolisUseProjectReferences=true
```

The interoperability contract runs the same Windows-host exchange twice:
once as an Android client and once as a Windows client. It verifies the
negotiated H.264, clipboard, display-selection, and adaptive-video commands
without requiring either device. The live Appium gate then exercises the
actual Android client against the configured Windows host and records the
first-frame budget in the timeline.

The Appium evidence run is environment-aware. Set
`NOVOLIS_REACH_UI_APP`, `NOVOLIS_REACH_UI_ENDPOINT`,
`NOVOLIS_REACH_UI_ANDROID_ENDPOINT`, optionally
`NOVOLIS_REACH_UI_TAILSCALE_ENDPOINT`, and
`NOVOLIS_REACH_UI_ANDROID_APK` for Android evidence; set
`NOVOLIS_REACH_UI_LOG` to a host or service log excerpt when available. Then
run:

```powershell
dotnet test d:\novolis\novolis-apps\tests\Reach.UiTests\Reach.UiTests.csproj -p:NovolisUseProjectReferences=true
```

Each configured run writes timestamped screenshots, telemetry, logs, and a
browsable `timeline.html` beside `timeline.md` and `timeline.json` through
the framework-neutral `Novolis.Testing.Timeline` package. Artifacts live under
`d:\novolis\novolis-apps\artifacts\Reach\usability-timeline`.
