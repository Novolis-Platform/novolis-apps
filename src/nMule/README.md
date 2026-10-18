# nMule

nMule is a .NET MAUI eD2K/Kad client for Android and Windows. The MVP is a
foreground client for loading editable HTTPS bootstrap sources, searching an
eD2K server, bootstrapping Kad, and downloading one verified eD2K part into
app-private storage.

## Network admission

nMule fails closed unless the host reports an active VPN transport. Android
uses the active network's VPN capability. Windows uses active tunnel/PPP
adapters plus conservative adapter, address, and Quad100 DNS signals.
Tailscale is classified separately and is blocked by default. The Settings
switch can allow Tailscale for the current session only; restarting nMule
clears that choice. Every HTTPS, TCP, and UDP operation passes through the
same gate. Cleartext HTTP bootstrap sources are rejected.

Security settings also provide a complete VPN-requirement override for
troubleshooting or networks where a VPN is unavailable. It is off by default,
session-only, and explicitly marked as not recommended; enabling it permits
ordinary Wi-Fi/mobile and Tailscale paths.

Android does not expose the active VPN application's package through the
public API, and Windows does not expose a universal VPN-owner API. nMule uses
conservative platform signals to recognize Tailscale-like paths. Ambiguous
Tailscale-like paths remain blocked unless the session-only override is
enabled.

## MVP limits

- No uploads, background transfers, callback hosting, or LowID callback path.
- Files larger than one eD2K part are shown but cannot be downloaded yet.
- LowID search results require a Kad source and a reachable one-part source.
- Completed files, `server.met`, and `nodes.dat` remain below the app-private
  profile directory. Windows does not register file associations or use
  public/shared storage.

## Local validation

```powershell
dotnet test d:\novolis\novolis-apps\tests\nMule.Unit\nMule.Unit.csproj -p:NovolisUseProjectReferences=true
dotnet build d:\novolis\novolis-apps\src\nMule\nMule.csproj -f net10.0-android -c Debug -p:NovolisUseProjectReferences=true -p:NovolisMauiTargetFrameworks=net10.0-android
dotnet build d:\novolis\novolis-apps\src\nMule\nMule.csproj -f net10.0-windows10.0.19041.0 -c Debug -p:NovolisUseProjectReferences=true -p:NovolisMauiTargetFrameworks=net10.0-windows10.0.19041.0
```

Use the repository's Android release workflow for signed APK generation.
