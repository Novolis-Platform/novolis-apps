# Merglyph privacy

Offline Markdown viewer host. No accounts, telemetry, or cloud sync.

## Ship / Local

| | |
|---|---|
| Ship | `android-apk` only |
| Local | Windows MAUI + Android |

## Android

| Item | Value |
|------|-------|
| applicationId | `dev.novolis.merglyph` |
| Permissions | None (INTERNET / ACCESS_NETWORK_STATE explicitly removed) |
| allowBackup | `false` |
| usesCleartextTraffic | `false` |
| Documents | System document picker / SAF |
| App state | App-private storage only |

## Windows (local debug)

| Item | Value |
|------|-------|
| App data | `%LOCALAPPDATA%\Novolis\merglyph\` |
| WebView2 | `%LOCALAPPDATA%\Novolis\merglyph\WebView2` |
| File associations | Opt-in via `MERGLYPH_REGISTER_FILE_ASSOCIATIONS=1`; cleanup via `MERGLYPH_UNREGISTER_FILE_ASSOCIATIONS=1` |

## Signing continuity

Release produces an installable APK even without persistent `ANDROID_KEYSTORE_*` secrets (adhoc CI keystore). In-place upgrades need the same key; uninstall/reinstall is expected between adhoc builds and versus standalone Merglyph releases from before the move into `novolis-apps`.

## Network destinations

None.
