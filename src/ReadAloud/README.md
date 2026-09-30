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

Open **Azure setup**, choose **Automatic — Microsoft sign-in**. The signed-in
directory user is the personal Microsoft account `frank.haugen@gmail.com`,
present in the tenant as a guest (`#EXT#`), not a work account. Sign-in
therefore does not call the Android broker and does not show the work-account
picker. The embedded WebView opens with `domain_hint=consumers` and that
login hint, so a corporate account already on the device is left unused.
Later token requests reuse that personal account. The redirect remains
`msalc8b938aa-2e5d-48b4-89c6-fc139733c44d://auth`.

Then choose a subscription only when more than one is available. Read Aloud
filters resource groups and services to compatible Speech resources and
automatically selects the only valid resource when there is one. The automatic
path does not ask for a Speech key or embed one as a secret in the APK.

The selected resource is configured with Microsoft Entra data-plane access.
MSAL owns the platform-native secure token cache.

The main screen shows two usage lines. **This device** counts characters sent,
Azure calls, cache replays, and failures for this install. **Azure usage —
last 30 days** is the resource total from Azure Monitor. It is loaded after
automatic sign-in and when you choose Refresh usage. Character totals and
call totals are separate Monitor queries (`interval=FULL`) because those
metrics do not share dimensions. The signed-in account needs Reader or
Monitoring Reader access. A subscription key can synthesize speech but cannot
read management-plane metrics, so desktop and manual setups still show the
device line only.

**Diagnostics** on the main screen are the recent journal lines for speech
activity and failures (character counts, voice, elapsed time, errors). The
spoken text is not written there. Open or share diagnostics also reveals the
underlying NDJSON file.

The main chrome **Voice** list is populated from the connected Speech resource
after sign-in, typed save, import, or Test. The default selection is
`en-US-AvaMultilingualNeural`. Changing the list persists that voice with the
saved Azure setup so the next launch restores it.

For **Manual — JSON credentials file**, type the Speech **endpoint** and
**subscription key** and choose **Save credentials**, or choose
**Import credentials file…** and select a user-owned UTF-8 JSON file with this
exact schema. Typed fields stay on the Manual panel even when import is
available. Import fills the endpoint and stores the key; the key box stays
empty after a successful save or import.

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
