# Release

## Model

1. Push/PR validates only affected apps from `build/apps.json` (no installers, no APKs).
2. Operators run **Release** (`workflow_dispatch`) with no inputs. The file calls `novolis-workflows` `apps-release.yml`, which builds every app on every channel declared in `ship`. One installer is a local publish (`scripts/build-installer.ps1`), not a GitHub Release.
3. Artifacts from that run land on a GitHub Release tagged `vYEAR.MAJOR.MINOR.BUILD` with `SHA256SUMS.txt`. Nothing is copied from an older tag. A release asset is the build for that commit.
4. `scripts/prune-github-releases.ps1` keeps the newest 5 releases. Each kept tag is a full drop, so deleting an older tag does not remove an installer that the latest tag lacks.

For apps with `update.enabled`, the same release also publishes a validated
`Novolis.Update.json` containing the app identity, channel, provenance,
target-specific assets, lengths, and SHA-256 hashes. The update document is
included in `SHA256SUMS.txt`; it is part of the release contract and must not
be uploaded separately or edited after publication.

Google Play and nuget.org are not part of this release. Play stays
`play-store.yml`, started by hand from an existing tag. nuget.org stays the
library workflow `dotnet-release-publish`. Either can become an input on this
release later. Neither runs now, and neither copies or replaces the GitHub
Release assets.

A full release builds only channels listed in `ship`. Reach is the only `linux-tar`. Apps that do not declare a channel do not get that artifact.

## Channels

### windows-inno

- Self-contained `win-x64` publish
- Per-user Inno (`PrivilegesRequired=lowest`) under `%LocalAppData%\Programs\Novolis\…`
- One Inno installer `.exe` per app
- Auxiliary runtime executables and portable payloads remain inside the installer; `SHA256SUMS.txt` covers published assets
- Each install includes `Novolis.Release.json` beside the program: display name, app id, version, git commit, repository, and the GitHub Release tag URL. The setup executable's Comments field repeats the name, version, channel, and a 12-character commit
- Stable `AppId` and `UsePreviousAppDir=yes` for upgrades, with multi-exe close filters where needed (Live Studio)

### android-apk

- Installable APK for apps with `android-apk` in `ship`
- Each app is signed with its own self-signed keystore. No certificate authority is involved
- GitHub secrets are named from `signingSecretKey`, for example `READALOUD_ANDROID_KEYSTORE_BASE64`, `READALOUD_ANDROID_KEY_ALIAS`, `READALOUD_ANDROID_KEYSTORE_PASSWORD`, and `READALOUD_ANDROID_KEY_PASSWORD`
- The same values live in the 1Password Environment **Novolis Android signing**, which is the recoverable copy
- Missing secrets fail the Android job. A freshly generated certificate cannot update an app already on a device
- Monotonic `versionCode` via `NovolisAndroidVersionCode` / `Get-NovolisAndroidVersionCode`
- Direct-release APKs are handed to Android's package installer by the updater.
  They must keep the same signing identity and a higher `versionCode`; store
  builds stay on the Play path.

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
pwsh -File d:\novolis\novolis-apps\scripts\build-installer.ps1 -App CadStudio3D -BuildNumber 1
```

## Signing notes

- GitHub Release APKs and Play upload bundles both use the app-specific key named from `signingSecretKey`.
- When those secrets are absent, the Android release job fails instead of minting a one-off certificate.
- Recover the keystore, alias, and passwords from the 1Password Environment **Novolis Android signing**. The certificate is self-signed.
- The selected GitHub Environment must contain
  `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON`. The service account must also be granted
  app-level release permissions in Play Console.
- Play App Signing's certificate must be registered in the Microsoft Entra
  Android application configuration when the app uses MSAL's default redirect.
- See `src/Merglyph/PRIVACY.md` for Merglyph signing continuity.
