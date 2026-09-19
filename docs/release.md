# Release

## Model

1. Push/PR validates only affected apps from `build/apps.json` (no installers, no APKs).
2. Operators run **Release** (`workflow_dispatch`) with:
   - `app`: a fixed dropdown of catalog apps, or `All`
   - `channel`: a fixed dropdown of `All`, `windows-inno`, or `android-apk`; with `All` apps selected, a specific channel filters to apps declaring it
3. Artifacts land on a GitHub Release tagged `vYEAR.MAJOR.MINOR.BUILD` with `SHA256SUMS.txt`.
4. `scripts/prune-github-releases.ps1` keeps the newest 5 releases.

Undeclared channels fail before workloads install. There is **no Linux release artifact** in phase one.

## Channels

### windows-inno

- Self-contained `win-x64` publish
- Per-user Inno (`PrivilegesRequired=lowest`) under `%LocalAppData%\Programs\Novolis\…`
- One Inno installer `.exe` per app
- Auxiliary runtime executables and portable payloads remain inside the installer; `SHA256SUMS.txt` covers published assets
- Stable `AppId` and `UsePreviousAppDir=yes` for upgrades, with multi-exe close filters where needed (Live Studio)

### android-apk

- Installable APK for apps with `android-apk` in `ship`
- Persistent `ANDROID_KEYSTORE_*` secrets are used when present
- Missing secrets produce an **adhoc-signed** APK for sideload testing (not upgrade-safe)
- Monotonic `versionCode` via `NovolisAndroidVersionCode` / `Get-NovolisAndroidVersionCode`

## Branding

Every app uses the Novolis mark at the repo root (`icon.png`, `icon.ico`, `logo-icon.svg`). Windows executables and Inno installers pick up `icon.ico` automatically. Avalonia window chrome loads `icon.png`. Android launcher icons come from `brand/android/ic_launcher.png` (MAUI hosts use `MauiIcon` from the same SVG). Regenerate rasters with:

```powershell
dotnet run --project d:\novolis\novolis-apps\tools\BrandAssets\BrandAssets.csproj -- d:\novolis\novolis-apps
```

## Local publish

```powershell
pwsh -File d:\novolis\novolis-apps\scripts\build-installer.ps1 -App DraftStudio -BuildNumber 1
```

## Signing notes

- Persistent `ANDROID_KEYSTORE_*` org secrets are preferred for upgrade-safe APKs.
- When those secrets are absent, Release still publishes an adhoc-signed APK for sideload testing. Uninstall/reinstall is expected between adhoc builds.
- See `src/Merglyph/PRIVACY.md` for Merglyph signing continuity.
