# Novolis Reach

Novolis Reach is a general-purpose remote interactive desktop product for
trusted networks. It attaches to an existing logged-in Windows interactive
session and transports live video, input, clipboard, audio, and later files.

Reach is deliberately separate from
[CursorRemote](d:\novolis\novolis-apps\src\CursorRemote). CursorRemote remains a
narrow Android controller for the Cursor window: it uses its own discovery,
HTTP, PNG, and input contract. Reach does not rename, migrate, or reuse that
implementation.

## Processes

| Process | Role |
| --- | --- |
| `Novolis.Reach.Host.Windows.Service` | Headless Windows service; accepts remote clients and owns lifecycle |
| `Novolis.Reach.Host.Windows.Session` | Headless helper in the logged-in interactive session |
| `Novolis.Reach.Host.Windows.Console` | Avalonia operator dashboard over local IPC |
| `Novolis.Reach.Client.Windows` | Windows client |
| `Novolis.Reach.Client.Linux` | Linux client |
| `Novolis.Reach.Client.Android` | Android client |

The service binds to loopback, private IPv4, and Tailscale IPv4 addresses.
Reach has no Novolis account, relay, or public authentication flow in this
personal-use shape.

The host uses TCP `19800` for control/input, TCP `19801` for encoded media,
and UDP `19802` for discovery. Clients fall back to the control stream when an
older host does not expose the media channel.

## Run locally

```powershell
dotnet run --project d:\novolis\novolis-apps\src\Reach\Reach.Host.Windows.Service\Reach.Host.Windows.Service.csproj -p:NovolisUseProjectReferences=true
dotnet run --project d:\novolis\novolis-apps\src\Reach\Reach.Host.Windows.Console\Reach.Host.Windows.Console.csproj -p:NovolisUseProjectReferences=true
dotnet run --project d:\novolis\novolis-apps\src\Reach\Reach.Client.Windows\Reach.Client.Windows.csproj -p:NovolisUseProjectReferences=true
```

Windows, Linux, and Android clients are day-one Reach clients. The release
catalog produces a Windows installer, a self-contained Linux tarball, and an
Android APK; the Windows installer also contains the Service, Session, and
Console host components and starts the host process at install and user-logon.

### Local protocol emulator

For client development without attaching to a real Windows session, run the
deterministic local host emulator:

```powershell
dotnet run --project d:\novolis\novolis-apps\src\Reach\Reach.Host.Windows.Emulator\Reach.Host.Windows.Emulator.csproj -p:NovolisUseProjectReferences=true -- --port 19800 --fps 12
```

It serves a generated H.264 desktop stream on `127.0.0.1:19800`, announces
itself through Reach discovery, and logs pointer, wheel, keyboard, text, and
clipboard input. The Android client translates a loopback discovery result to
`10.0.2.2:19800`, which is the Android emulator route to the Windows host.

To build a packaged APK and install it on a booted Android emulator:

```powershell
dotnet publish d:\novolis\novolis-apps\src\Reach\Reach.Client.Android\Reach.Client.Android.csproj -p:NovolisUseProjectReferences=true -f net10.0-android -c Debug -p:AndroidPackageFormats=apk -p:AndroidBuildApplicationPackage=true -p:AndroidFastDeployment=false -p:EmbedAssembliesIntoApk=true
& "$env:ANDROID_HOME\platform-tools\adb.exe" install -r d:\novolis\novolis-apps\artifacts\publish\Reach.Client.Android\debug\com.novolis.reach-Signed.apk
```

Launch Reach, allow discovery to finish, and connect to the local emulator.

Reach's protocol is product-private. Generic framing, datagrams, discovery,
Tailscale binding, Windows capabilities, and video codecs live in their
respective Novolis libraries and do not contain Reach types.
