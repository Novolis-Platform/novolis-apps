# Novolis Torrent

Novolis Torrent is the Windows and Android product host for the Novolis torrent stack. It
combines:

- Public Magnetz search by default, or optional Torznab search, with seeders,
  leechers, ratio, availability, and size filters.
- Magnet copy/open actions and downloadable `.torrent` result import.
- Foreground torrent transfers from the platform file picker.
- Maintained tracker-list discovery through newTrackon, ngosang, and XIU2.
- App-private storage with no shared-storage permission.

The app keeps transfer sessions active while it is open. It does not start
background transfers or silently grant storage access.

## Local Android build

```powershell
dotnet build d:\novolis\novolis-apps\src\NovolisTorrent\NovolisTorrent.csproj -f net10.0-android -c Debug -p:NovolisUseProjectReferences=true -p:NovolisMauiTargetFrameworks=net10.0-android
```

## Local Windows build

```powershell
dotnet build d:\novolis\novolis-apps\src\NovolisTorrent\NovolisTorrent.csproj -f net10.0-windows10.0.19041.0 -c Debug -p:NovolisUseProjectReferences=true -p:NovolisMauiTargetFrameworks=net10.0-windows10.0.19041.0
```

The optional Torznab endpoint is stored in platform preferences and its API
key is stored with platform secure storage. Leave the endpoint empty to use
the public Magnetz metadata API. Use only content and sources you are
authorized to access.
