# Books Mobile

Avalonia **Android + Windows** companion for [frankhaugen/books](https://github.com/frankhaugen/books): **Sign in with GitHub** (passkey / GitHub app), sparse `content/` mirror, markdown editing, chapter TTS, **Pull** and **Save/Commit/Push**.

**Not** in the Windows installer / release catalog — local deploy only.

## Projects

| Project | Path | Role |
|---------|------|------|
| `BooksMobile` | `BooksMobile/` | Shared UI, GitHub session, markdown editor, voice services |
| `BooksMobile.Desktop` | `BooksMobile.Desktop/` | Windows head (`WinExe`) |
| `BooksMobile.Android` | `BooksMobile.Android/` | Android APK (`net10.0-android`, API 23+) |

## Platforms

| Target | SDK / RID | Notes |
|--------|-----------|-------|
| Windows desktop | Avalonia Desktop | Primary dev loop |
| Android | `net10.0-android` | Deploy via `adb`; APK not CI-released |

## Run (Desktop)

```powershell
pwsh -File scripts/run-booksmobile-desktop.ps1
```

Local workspace (no GitHub sign-in):

```powershell
pwsh -File scripts/run-booksmobile-desktop.ps1 -LocalReview
pwsh -File scripts/run-booksmobile-desktop.ps1 -LocalWorkspace "D:\path\to\repo"
```

Or directly:

```powershell
dotnet run --project src/BooksMobile/BooksMobile.Desktop
```

## Deploy (Android)

```powershell
pwsh -File scripts/deploy-booksmobile-android.ps1 -Serial <device-serial>
```

Requires Android SDK / workload and a connected device or emulator.

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
dotnet build src/BooksMobile/BooksMobile.Desktop -p:NovolisUseProjectReferences=true
```

Committed builds use **NuGet-only** (GitHub Packages + nuget.org). No local folder feeds.
