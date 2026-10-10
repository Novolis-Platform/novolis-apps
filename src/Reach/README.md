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
dotnet publish d:\novolis\novolis-apps\src\Reach\Reach.Client.Android\Reach.Client.Android.csproj -p:NovolisUseProjectReferences=true -f net10.0-android -c Debug -p:AndroidPackageFormats=apk -p:AndroidBuildApplicationPackage=true -p:AndroidFastDeployment=false -p:EmbedAssembliesIntoApk=true
& "$env:ANDROID_HOME\platform-tools\adb.exe" install -r d:\novolis\novolis-apps\artifacts\publish\Reach.Client.Android\debug\com.novolis.reach-Signed.apk
```

Launch Reach, allow discovery to finish, and connect to the Windows host.

Reach's protocol is product-private. Generic framing, datagrams, discovery,
Tailscale binding, Windows capabilities, and video codecs live in their
respective Novolis libraries and do not contain Reach types.
