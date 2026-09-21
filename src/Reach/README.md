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

The service binds only to configured Tailscale IPv4 addresses. Reach has no
Novolis account, relay, or public authentication flow in this personal-use
shape.

## Run locally

```powershell
dotnet run --project d:\novolis\novolis-apps\src\Reach\Reach.Host.Windows.Service\Reach.Host.Windows.Service.csproj -p:NovolisUseProjectReferences=true
dotnet run --project d:\novolis\novolis-apps\src\Reach\Reach.Host.Windows.Console\Reach.Host.Windows.Console.csproj -p:NovolisUseProjectReferences=true
dotnet run --project d:\novolis\novolis-apps\src\Reach\Reach.Client.Windows\Reach.Client.Windows.csproj -p:NovolisUseProjectReferences=true
```

Reach's protocol is product-private. Generic framing, datagrams, discovery,
Tailscale binding, Windows capabilities, and video codecs live in their
respective Novolis libraries and do not contain Reach types.
