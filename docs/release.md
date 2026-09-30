# Release

## Model

1. Push/PR validates only affected apps from `build/apps.json` (no installers, no APKs).
2. Operators run **Release** (`workflow_dispatch`) with:
   - `app`: a fixed dropdown of catalog apps, or `All`
   - `channel`: a fixed dropdown of `All`, `windows-inno`, or `android-apk`; with `All` apps selected, a specific channel filters to apps declaring it
3. Artifacts land on a GitHub Release tagged `vYEAR.MAJOR.MINOR.BUILD` with `SHA256SUMS.txt`.
4. `scripts/prune-github-releases.ps1` keeps the newest 5 releases.

Google Play delivery is a separate manual workflow. It starts from an existing
GitHub Release tag and builds the matching signed Android App Bundle, so adding
Play Store delivery does not remove or change GitHub Release assets.

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
- Persistent `ANDROID_KEYSTORE_*` secrets sign every GitHub Release APK
- Missing secrets fail the Android job. A freshly generated certificate cannot update an app already on a device
- Monotonic `versionCode` via `NovolisAndroidVersionCode` / `Get-NovolisAndroidVersionCode`

### Google Play

- Enabled per app with `release.googlePlay.enabled` in `build/apps.json`
- Run `.github/workflows/play-store.yml` after the matching GitHub Release exists
- Produces a signed `.aab`; persistent upload-key secrets are mandatory
- Uploads to `internal`, `closed`, `open`, or `production`
- Production can use a staged rollout with `user_fraction`; non-production tracks must complete
- Promotion is protected by GitHub Environments named `google-play-internal`,
  `google-play-closed`, `google-play-open`, and `google-play-production`
- The existing `android-apk` channel remains the sideload/GitHub Release path

## Branding

Every app uses the Novolis mark at the repo root (`icon.png`, `icon.ico`, `logo-icon.svg`). Windows executables and Inno installers pick up `icon.ico` automatically. Avalonia window chrome loads `icon.png`. Avalonia Android launchers use `brand/android/ic_launcher.png`. MAUI hosts (Merglyph) use PNG `MauiIcon` / splash rasters — the gradient SVG is not a valid Android launcher source. Regenerate rasters with:

```powershell
dotnet run --project d:\novolis\novolis-apps\tools\BrandAssets\BrandAssets.csproj -- d:\novolis\novolis-apps
```

## Local publish

```powershell
pwsh -File d:\novolis\novolis-apps\scripts\build-installer.ps1 -App DraftStudio -BuildNumber 1
```

## Signing notes

- Persistent `ANDROID_KEYSTORE_*` secrets sign GitHub Release APKs so later downloads update the installed app.
- When those secrets are absent, the Android release job fails instead of minting a one-off certificate.
- Google Play never uses the adhoc fallback. It requires the app-specific upload
  key named from the manifest `signingSecretKey`, for example
  `READALOUD_ANDROID_KEYSTORE_BASE64`, `READALOUD_ANDROID_KEY_ALIAS`,
  `READALOUD_ANDROID_KEYSTORE_PASSWORD`, and `READALOUD_ANDROID_KEY_PASSWORD`.
- The selected GitHub Environment must contain
  `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON`. The service account must also be granted
  app-level release permissions in Play Console.
- Play App Signing's certificate must be registered in the Microsoft Entra
  Android application configuration when the app uses MSAL's default redirect.
- See `src/Merglyph/PRIVACY.md` for Merglyph signing continuity.
