# Read Aloud

Avalonia **Android + Windows desktop** scratch reader: paste or open text, **listen** with the device voice or a user-owned Azure Speech resource, or **save an MP3**.

No GitHub, no library, no manuscript tree — just text in, speech out. Uses `Novolis.Avalonia.Speech` with local platform adapters and `Novolis.Audio.Voice.AzureSpeech`.

**Windows** ships on [GitHub Releases](https://github.com/Novolis-Platform/novolis-apps/releases) as a per-user Inno installer and portable zip. **Android APK** is local deploy only (not CI-released).

## Projects

| Project | Path | Role |
|---------|------|------|
| `ReadAloud` | `ReadAloud/` | Shared UI + speech service |
| `ReadAloud.Desktop` | `ReadAloud.Desktop/` | Windows desktop head (`WinExe`) — release catalog |
| `ReadAloud.Android` | `ReadAloud.Android/` | Android APK (`net10.0-android`, API 23+) — local only |

## Platforms

| Target | SDK / RID | Notes |
|--------|-----------|-------|
| Windows desktop | Avalonia Desktop | Installer + portable zip on merge to `main` |
| Android | `net10.0-android` | Deploy via `adb`; APK not CI-released |

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

Requires Android SDK / workload and a connected device or emulator. APK is never uploaded to GitHub Releases. Device voice works offline; Azure Speech and MP3 export use the endpoint and quota supplied by the user.

## Android Azure sign-in

The Android host uses a single-tenant Microsoft Entra public client and the
system browser. Open **Azure setup**, choose **Sign in with Microsoft**, then
choose a subscription only when more than one is available. Read Aloud filters
resource groups and services to compatible Speech resources and automatically
selects the only valid resource when there is one. No Speech key, endpoint, or
client id is entered by the user or embedded as a secret in the APK.

The selected resource is configured with Microsoft Entra data-plane access.
MSAL owns the platform-native secure token cache. The
single-tenant app registration uses the MSAL redirect
`msalc8b938aa-2e5d-48b4-89c6-fc139733c44d://auth`.

For developer-only testing, an optional untracked fallback can be installed
from `local/ReadAloud/azure-speech-fallback.json`. It must contain an HTTPS
endpoint and Speech subscription key:

```json
{
  "endpoint": "https://your-resource.cognitiveservices.azure.com/",
  "subscriptionKey": "developer-only-key",
  "voiceName": "en-US-AvaMultilingualNeural",
  "locale": "en-US"
}
```

The file is copied into Android app-private storage, never packaged into the
APK, and loaded only after pressing **Import credentials** in **Azure setup**.

## Packages

Consumes GitHub Packages `2026.1.*`:

| Package | Role |
|---------|------|
| `Novolis.Avalonia.Mobile` / `.Desktop` / `.Android` | Cross-platform shell + app-private storage |
| `Novolis.Avalonia.Speech` | Provider selection, secure Azure setup, and capability-aware operations |
| `Novolis.Audio.Voice.AzureSpeech` | Thin Azure Speech client returning MP3 |
| `Novolis.Audio.Voice.Platform.Android` / `.Windows` | Local device voice playback |
| `Novolis.Manuscript.Export.Audio` | Speech planner, synthesizer, desktop MP3 player |

## Local development (ProjectReference mode)

```powershell
dotnet build d:\novolis\novolis-apps\src\ReadAloud\ReadAloud.Desktop -p:NovolisUseProjectReferences=true
```

Committed builds use **NuGet-only** (GitHub Packages + nuget.org). No local folder feeds.
