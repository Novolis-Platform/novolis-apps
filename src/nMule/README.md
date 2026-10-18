# nMule

nMule is a new Android-first .NET MAUI eD2K/Kad client. The MVP is a
foreground client for loading editable HTTPS bootstrap sources, searching an
eD2K server, bootstrapping Kad, and downloading one verified eD2K part into
app-private storage.

## Network admission

nMule fails closed unless Android reports an active VPN transport. Tailscale
is classified separately and is blocked by default. The Settings switch can
allow Tailscale for the current session only; restarting nMule clears that
choice. Every HTTPS, TCP, and UDP operation passes through the same gate.
Cleartext HTTP bootstrap sources are rejected.

Android does not expose the active VPN application's package through the
public API, so nMule uses conservative interface, address, and Quad100 DNS
signals to recognize Tailscale-like paths. Ambiguous Tailscale-like paths
remain blocked unless the session-only override is enabled.

## MVP limits

- No uploads, background transfers, callback hosting, or LowID callback path.
- Files larger than one eD2K part are shown but cannot be downloaded yet.
- LowID search results require a Kad source and a reachable one-part source.
- Completed files, `server.met`, and `nodes.dat` remain below the app-private
  profile directory.

## Local validation

```powershell
dotnet test d:\novolis\novolis-apps\tests\nMule.Unit\nMule.Unit.csproj -p:NovolisUseProjectReferences=true
dotnet build d:\novolis\novolis-apps\src\nMule\nMule.csproj -f net10.0-android -c Debug -p:NovolisUseProjectReferences=true -p:NovolisMauiTargetFrameworks=net10.0-android
```

Use the repository's Android release workflow for signed APK generation.
