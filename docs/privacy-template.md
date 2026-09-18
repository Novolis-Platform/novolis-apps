# Privacy / data inventory template

Copy to `src/<App>/PRIVACY.md` (or `docs/privacy/<app-key>.md`) for every shipped product.

## Product

- **App key:**
- **Display name:**
- **Ship channels:**

## Permissions

| Platform | Permission | Why |
|----------|------------|-----|
| Android | (none / INTERNET / …) | |
| Windows | (file associations? none by default) | |

## Network

| Destination | Purpose |
|-------------|---------|
| (none) | |

## Storage

| Kind | Location | Retention |
|------|----------|-----------|
| App data | `%LOCALAPPDATA%\Novolis\<app-key>\` | Survives uninstall |
| Cache | `…\cache` | Safe to delete |
| Install payload | `%LOCALAPPDATA%\Programs\Novolis\…` | Removed on uninstall |
| User documents | User-selected paths | Never deleted by uninstall |

## Backup

- Android `allowBackup`: false / true (justify)
- Credentials namespace:

## Deletion

- Sign-out / credential clear:
- Uninstall preserves user data by default; explicit purge (if any):
