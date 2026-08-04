# Books Mobile

Avalonia **Android + Windows** companion for [frankhaugen/books](https://github.com/frankhaugen/books): **Sign in with GitHub** (passkey / GitHub app), sparse `content/` mirror, markdown editing, chapter TTS, **Pull** and **Save/Commit/Push**.

**Windows** ships on [GitHub Releases](https://github.com/Novolis-Platform/novolis-apps/releases) as a per-user Inno installer and portable zip. **Android APK** is local deploy only (not CI-released).

## Projects

| Project | Path | Role |
|---------|------|------|
| `BooksMobile` | `BooksMobile/` | Shared UI, GitHub session, markdown editor, voice services |
| `BooksMobile.Desktop` | `BooksMobile.Desktop/` | Windows head (`WinExe`) — release catalog |
| `BooksMobile.Android` | `BooksMobile.Android/` | Android APK (`net10.0-android`, API 23+) — local only |

## Platforms

| Target | SDK / RID | Notes |
|--------|-----------|-------|
| Windows desktop | Avalonia Desktop | Installer + portable zip on merge to `main` |
| Android | `net10.0-android` | Deploy via `adb`; APK not CI-released |

## Releases (Windows)

Published on merge to `main` as `BooksMobileSetup-{version}-win-x64.exe` and `BooksMobile-{version}-win-x64.zip`. Per-user install under `%LOCALAPPDATA%\Programs\Novolis\Books Mobile`. See [novolis-apps release catalog](../../README.md#releases).

```powershell
pwsh -File d:\novolis\novolis-apps\scripts\build-installer.ps1 -App BooksMobile
```

## Run (Desktop)

```powershell
pwsh -File d:\novolis\novolis-apps\scripts\run-booksmobile-desktop.ps1
```

Local workspace (no GitHub sign-in):

```powershell
pwsh -File d:\novolis\novolis-apps\scripts\run-booksmobile-desktop.ps1 -LocalReview
pwsh -File d:\novolis\novolis-apps\scripts\run-booksmobile-desktop.ps1 -LocalWorkspace "D:\path\to\repo"
```

Or directly:

```powershell
dotnet run --project d:\novolis\novolis-apps\src\BooksMobile\BooksMobile.Desktop
```

## Deploy (Android)

```powershell
pwsh -File d:\novolis\novolis-apps\scripts\deploy-booksmobile-android.ps1 -Serial <device-serial>
```

Requires Android SDK / workload and a connected device or emulator. APK is never uploaded to GitHub Releases.

## Sign-in (passkey)

Login uses GitHub OAuth **Device Flow**: the app opens GitHub in the browser/Custom Tabs; you approve with a passkey, GitHub app, or password. You never paste a PAT.

GitHub requires a public **OAuth App Client ID** (not a secret):

1. [New OAuth App](https://github.com/settings/applications/new) — name `Novolis BooksMobile`, enable **Device Flow**
2. Copy the **Client ID** into `BooksMobileOptions.DefaultGitHubClientId` or set `BOOKSMOBILE_GITHUB_CLIENT_ID`

After that, the app only shows **Sign in with GitHub**.

## Packages

Consumes GitHub Packages `2026.1.*`:

| Package | Role |
|---------|------|
| `Novolis.Avalonia.Mobile` / `.Desktop` / `.Android` | Cross-platform shell |
| `Novolis.Avalonia.Markdown` | Editor + preview |
| `Novolis.IO.GitHub` | Device-flow auth, sparse clone, commit/push |
| `Novolis.Markup.Manuscript` | Manuscript `content/` layout |
| `Novolis.Audio.Voice.Manuscript` / `.EdgeTts` | Chapter speech |

## Local development (ProjectReference mode)

For sibling Mobile/Markdown/IO packages, open **`Novolis.Platform.slnx`** or:

```powershell
dotnet build d:\novolis\novolis-apps\src\BooksMobile\BooksMobile.Desktop -p:NovolisUseProjectReferences=true
```

Committed builds use **NuGet-only** (GitHub Packages + nuget.org). No local folder feeds.
