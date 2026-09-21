# Cursor Remote

Cursor Remote is a personal Android controller for the Cursor window running on
the Windows PC. The phone talks only to the Windows host over Tailscale
(`:18790` + pairing token). The host captures the live unlocked console
(Cursor window when available, otherwise the primary monitor) and injects
input there.

Provides:

- screen snapshots of the Cursor window / primary monitor;
- pinch-zoom and pan on the phone view;
- tap-to-click input;
- text and common key input;
- a Cursor focus command (maximizes the window);
- per-run token pairing.

The Windows host binds only to IPv4 Tailscale addresses. It does not open a
public relay or send screen data through a Novolis service. The Windows session
must remain unlocked because this is direct screen and input control.

## Run the Windows host

```powershell
dotnet run --project d:\novolis\novolis-apps\src\CursorRemote\CursorRemote.Desktop\CursorRemote.Desktop.csproj -p:NovolisUseProjectReferences=true
```

The host displays its Tailscale endpoint and a token. Enter both in the Android
controller.

If Windows Firewall blocks the port, allow TCP 18790 only from the Tailscale
range:

```powershell
New-NetFirewallRule -DisplayName "Cursor Remote (Tailscale)" -Direction Inbound -Action Allow -Protocol TCP -LocalPort 18790 -Profile Any -RemoteAddress 100.64.0.0/10
```

## Build and install Android

```powershell
dotnet build d:\novolis\novolis-apps\src\CursorRemote\CursorRemote.Android\CursorRemote.Android.csproj -c Release -p:NovolisUseProjectReferences=true
adb install -r <path-to-CursorRemote.Android.apk>
```

The Android client permits cleartext HTTP because the endpoint is intended to
be reachable only through Tailscale. The pairing token is still required for
every API request.

## Boundary

This controls the visible Cursor window on the unlocked console; it is not an
RDP session, not a Cursor agent-session API, and does not attach to the current
chat conversation.
