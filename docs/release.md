# Release

## Model

1. Push/PR validates only affected apps from `build/apps.json` (no installers, no APKs).
2. Operators run **Release** (`workflow_dispatch`) with:
   - `app`: a fixed dropdown of catalog apps, or `All`
   - `channel`: a fixed dropdown of `All`, `windows-inno`, or `android-apk` (must be in that app's `ship` list)
3. Artifacts land on a GitHub Release tagged `vYEAR.MAJOR.MINOR.BUILD` with `SHA256SUMS.txt`.
4. `scripts/prune-github-releases.ps1` keeps the newest 5 releases.

Undeclared channels fail before workloads install. There is **no Linux release artifact** in phase one.

## Channels

### windows-inno

- Self-contained `win-x64` publish
- Per-user Inno (`PrivilegesRequired=lowest`) under `%LocalAppData%\Programs\Novolis\…`
- Portable zip + SHA-256
- Stable `AppId`, `AllowDowngrades=no`, multi-exe close filters where needed (Live Studio)

### android-apk

- Signed APK only for apps with `android-apk` in `ship`
- Persistent `ANDROID_KEYSTORE_*` secrets required (adhoc keys are rejected on release)
- Monotonic `versionCode` via `NovolisAndroidVersionCode` / `Get-NovolisAndroidVersionCode`

## Local publish

```powershell
pwsh -File d:\novolis\novolis-apps\scripts\build-installer.ps1 -App DraftStudio -BuildNumber 1
```

## Signing notes

- Books Mobile / Read Aloud / Merglyph Android releases require org secrets.
- Merglyph’s pre-migration standalone releases may have used an ephemeral CI keystore; the first migrated APK may require reinstall until the persistent key is confirmed. See `src/Merglyph/PRIVACY.md`.
