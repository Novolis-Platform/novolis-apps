# Read Aloud

Avalonia **Android + Windows desktop** scratch reader: paste or open text, **listen** with a user-owned Azure Speech resource, or **save an MP3**.

No GitHub, no library, no manuscript tree — just text in, speech out. Uses `Novolis.Avalonia.Speech` with local platform adapters and `Novolis.Audio.Voice.AzureSpeech`.

**Windows** ships on [GitHub Releases](https://github.com/Novolis-Platform/novolis-apps/releases) as a per-user Inno installer and portable zip. The **Android APK** remains a GitHub Release/sideload artifact, and the matching signed **Android App Bundle** can be delivered to Google Play through the separate Play Store workflow.

## Projects

| Project | Path | Role |
|---------|------|------|
| `ReadAloud` | `ReadAloud/` | Shared UI + speech service |
| `ReadAloud.Desktop` | `ReadAloud.Desktop/` | Windows desktop head (`WinExe`) — release catalog |
| `ReadAloud.Android` | `ReadAloud.Android/` | Android APK/AAB (`net10.0-android`, API 23+) |

## Platforms

| Target | SDK / RID | Notes |
|--------|-----------|-------|
| Windows desktop | Avalonia Desktop | Installer + portable zip on merge to `main` |
| Android | `net10.0-android` | Deploy via `adb`, GitHub Release APK, or Google Play AAB |

## Releases (Windows)

Published on merge to `main` as `ReadAloudSetup-{version}-win-x64.exe` and `ReadAloud-{version}-win-x64.zip`. Per-user install under `%LOCALAPPDATA%\Programs\Novolis\Read Aloud`. See [novolis-apps release catalog](../../README.md#releases).

```powershell
pwsh -File d:\novolis\novolis-apps\scripts\build-installer.ps1 -App ReadAloud
```

## Run (Desktop)

```powershell
pwsh -File d:\novolis\novolis-apps\scripts\run-readaloud-desktop.ps1
```

Or directly:

```powershell
dotnet run --project d:\novolis\novolis-apps\src\ReadAloud\ReadAloud.Desktop
```

## Deploy (Android)

```powershell
pwsh -File d:\novolis\novolis-apps\scripts\deploy-readaloud-android.ps1 -Serial <device-serial>
```

Requires Android SDK / workload and a connected device or emulator. Read Aloud
uses Azure Speech for playback and MP3 export; the endpoint and quota are
supplied by the user.

The Google Play workflow starts from an existing `vYEAR.MAJOR.MINOR.BUILD`
GitHub Release tag and uploads the matching signed AAB to the selected Play
track. See [`docs/release.md`](../../docs/release.md) for the required upload
key and service-account setup.

## Android Azure sign-in

The Android host uses a single-tenant Microsoft Entra public client and the
system browser. Open **Azure setup**, choose **Automatic — Microsoft sign-in**,
then choose a subscription only when more than one is available. Read Aloud
filters resource groups and services to compatible Speech resources and
automatically selects the only valid resource when there is one. The automatic
path does not ask for a Speech key or embed one as a secret in the APK.

The selected resource is configured with Microsoft Entra data-plane access.
MSAL owns the platform-native secure token cache. The
single-tenant app registration uses the MSAL redirect
`msalc8b938aa-2e5d-48b4-89c6-fc139733c44d://auth`.

Automatic sign-in also enables the **Azure usage — last 30 days** view. It
reads the `SynthesizedCharacters` and request/error totals from Azure Monitor
through the management API. The signed-in account needs Reader or Monitoring
Reader access to the selected resource. Manual subscription-key credentials
can synthesize speech but cannot read management-plane usage metrics.

For **Manual — JSON credentials file**, choose **Import credentials file…**
and select a user-owned UTF-8 JSON file with this exact schema:

```json
{
  "schema": "novolis.readaloud.azure-speech-credentials",
  "version": 1,
  "authentication": "subscriptionKey",
  "endpoint": "https://your-resource.cognitiveservices.azure.com/",
  "subscriptionKey": "your-speech-resource-key",
  "voiceName": "en-US-AvaMultilingualNeural",
  "locale": "en-US"
}
```

`schema`, `version`, `authentication`, `endpoint`, and `subscriptionKey` are
required. `voiceName` and `locale` are optional. The selected file is read
once; it is not copied into or packaged with the app. The imported key is
stored only in platform secure storage.

## Packages

Consumes GitHub Packages `2026.1.*`:

| Package | Role |
|---------|------|
| `Novolis.Avalonia.Mobile` / `.Desktop` / `.Android` | Cross-platform shell + app-private storage |
| `Novolis.Avalonia.Speech` | Provider selection, secure Azure setup, and capability-aware operations |
| `Novolis.Audio.Voice.AzureSpeech` | Thin Azure Speech client returning MP3 |
| `Novolis.Audio.Voice.Platform.Android` / `.Windows` | Host audio playback for Azure MP3 |
| `Novolis.Manuscript.Export.Audio` | Speech planner, synthesizer, desktop MP3 player |

## Local development (ProjectReference mode)

```powershell
dotnet build d:\novolis\novolis-apps\src\ReadAloud\ReadAloud.Desktop -p:NovolisUseProjectReferences=true
```

Committed builds use **NuGet-only** (GitHub Packages + nuget.org). No local folder feeds.
