# Novolis PDF Reader privacy

Offline PDF reader host. No accounts, telemetry, or cloud sync.

## Ship / Local

| | |
|---|---|
| Ship | `android-apk` only |
| Local | Windows MAUI + Android |

## Android

| Item | Value |
|------|-------|
| applicationId | `com.novolis.pdfreader` |
| Permissions | None (INTERNET / ACCESS_NETWORK_STATE explicitly removed) |
| allowBackup | `false` |
| usesCleartextTraffic | `false` |
| Documents | System document picker / SAF / `ACTION_VIEW` |
| App state | App-private storage only |

## Windows (local debug)

| Item | Value |
|------|-------|
| App data | `%LOCALAPPDATA%\Novolis\pdf-reader\` |
| File associations | Opt-in **Add to Open With** on the welcome screen; current-user hive only |

## Signing continuity

Release produces an installable APK even without persistent `ANDROID_KEYSTORE_*` secrets (adhoc CI keystore). In-place upgrades need the same key; uninstall/reinstall is expected between adhoc builds.

## Network destinations

None.
