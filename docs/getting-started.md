# Getting started

## Prerequisites

- .NET SDK 10.0.100+ (`global.json`)
- GitHub CLI for GPR restore (`configure-gpr-user-nuget.ps1`)
- Desktop environment for Avalonia

## Build

```powershell
cd novolis-apps
..\novolis-governance\scripts\configure-gpr-user-nuget.ps1
dotnet restore
dotnet build --no-restore
```

## Install (Windows)

Download only from official [GitHub Releases](https://github.com/Novolis-Platform/novolis-apps/releases) (`Novolis-Platform/novolis-apps`):

| Asset | Use |
|-------|-----|
| `BooksWriterStudioSetup-*-win-x64.exe` | **Books Writer Studio installer** |
| `DraftStudioSetup-*-win-x64.exe` | **Draft Studio installer** — `%LOCALAPPDATA%\Programs\Novolis\Draft Studio` |
| `DraftStudio-*-win-x64.zip` | Draft Studio portable |
| `SinsOfACapitalismTycoonSetup-*-win-x64.exe` | **Sins of a Capitalism Tycoon installer** |
| `LiveStudioSetup-*-win-x64.exe` | **Live Studio installer** |
| `SHA256SUMS.txt` | SHA-256 hashes for all zip and setup exe files on each release |

### Verify downloads

Before running the installer, verify the SHA-256 hash:

```powershell
Get-FileHash .\DraftStudioSetup-*-win-x64.exe -Algorithm SHA256
# Compare with the matching line in SHA256SUMS.txt from the same release
```

### SmartScreen (unsigned installer)

Installers are not yet Authenticode-signed. Windows SmartScreen may show **"Windows protected your PC"** on first download. This is expected until code signing is added. To proceed: **More info** → **Run anyway**. Only install builds downloaded from the official releases page above.

New releases are created automatically when changes merge to `main` (see Merge workflow).

Build installer locally (requires Inno Setup 6 for the setup exe):

```powershell
pwsh -File scripts/build-installer.ps1 -App All
# Single app:
pwsh -File scripts/build-installer.ps1 -App DraftStudio
# Skip Inno compile (publish + zip only):
pwsh -File scripts/build-installer.ps1 -App All -SkipInstaller
```

## Draft Studio

```powershell
dotnet run --project src/DraftStudio
# Headless pipeline check (DSL → .cadjson → .cadphys.json):
dotnet run --project src/DraftStudio -- --smoke
```

Command-driven CAD-light: type `Line(0,0,2,0)`, `Circle(0,0,5)`, `Spline(0,0,1,1,2,0)`, `Box(1,1,1)` in the command bar, or use **Line / Circle / Rect / Spline** tools (same commands). Toggle **Draft** (XZ plan canvas) and **Model** (Raylib orbit). **Export Phys** writes `draft.cadphys.json`.

- Workspace: `%LocalAppData%\Novolis\Draft Studio\default-workspace\draft.cadjson`
- Formats: [cadjson.md](../../novolis-governance/docs/cadjson.md)
- Shortcuts: `Ctrl+S` save, `Ctrl+Z` / `Ctrl+Y` undo/redo, `Del` delete, `F` fit, `Esc` cancel tool, `Enter` finish spline

## Books Writer Studio

```powershell
dotnet run --project src/BooksWriterStudio
```

## Sins of a Capitalism Tycoon

```powershell
dotnet run --project src/SinsOfACapitalismTycoon -- --mode headless
dotnet run --project src/SinsOfACapitalismTycoon -- --mode avalonia
```

## Live Studio

```powershell
dotnet run --project src/LiveStudio/studio
```
