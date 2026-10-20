# Novolis Torrent

Novolis Torrent is the Android product host for the Novolis torrent stack. It
combines:

- Torznab search with seeders, leechers, ratio, availability, and size filters.
- Magnet copy/open actions and downloadable `.torrent` result import.
- Foreground torrent transfers from Android's file picker.
- Maintained tracker-list discovery through newTrackon, ngosang, and XIU2.
- App-private storage with no shared-storage permission.

The app keeps transfer sessions active while it is open. It does not start
background transfers or silently grant storage access.

## Local Android build

```powershell
dotnet build d:\novolis\novolis-apps\src\NovolisTorrent\NovolisTorrent.csproj -f net10.0-android -c Debug -p:NovolisUseProjectReferences=true -p:NovolisMauiTargetFrameworks=net10.0-android
```

Set `NOVOLIS_TORRENT_TORZNAB_ENDPOINT` and
`NOVOLIS_TORRENT_TORZNAB_API_KEY` only for the Windows utility. The Android app
stores its endpoint in preferences and its optional API key with Android
Secure Storage.
