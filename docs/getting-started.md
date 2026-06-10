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
| `ManuscriptStudioSetup-*-win-x64.exe` | **Manuscript Studio installer** — `%LOCALAPPDATA%\Programs\Novolis\Manuscript Studio` |
| `ManuscriptStudio-*-win-x64.zip` | Manuscript Studio portable |
| `ConceptStudioSetup-*-win-x64.exe` | **Concept Studio installer** — `%LOCALAPPDATA%\Programs\Novolis\Concept Studio` |
| `ConceptStudio-*-win-x64.zip` | Concept Studio portable |
| `SHA256SUMS.txt` | SHA-256 hashes for all zip and setup exe files on each release |

### Verify downloads

Before running the installer, verify the SHA-256 hash:

```powershell
Get-FileHash .\ManuscriptStudioSetup-*-win-x64.exe -Algorithm SHA256
# Compare with the matching line in SHA256SUMS.txt from the same release
```

### SmartScreen (unsigned installer)

Installers are not yet Authenticode-signed. Windows SmartScreen may show **"Windows protected your PC"** on first download. This is expected until code signing is added. To proceed: **More info** → **Run anyway**. Only install builds downloaded from the official releases page above.

New releases are created automatically when changes merge to `main` (see Merge workflow).

Build installer locally (requires Inno Setup 6 for the setup exe):

```powershell
pwsh -File scripts/build-installer.ps1 -App All
# Single app:
pwsh -File scripts/build-installer.ps1 -App ConceptStudio
# Skip Inno compile (publish + zip only):
pwsh -File scripts/build-installer.ps1 -App All -SkipInstaller
```

## Concept Studio

```powershell
dotnet run --project src/ConceptStudio
```

Block out ships and props with boxes, cylinders, cones, spheres, and wedges. Use **Plan / Profile / Bow** in the view combo for orthographic technical views, **Dimension** for callouts, and **Export SVG** or **Export PNG** for book art.

- Workspace: `%LocalAppData%\Novolis\Concept Studio\default-workspace\concept.json`
- **Preview** = instant Raylib editing; **Quality** = path-traced render
- Shortcuts: `B` box, `C` cylinder, `N` cone, `S` sphere, `Ctrl+S` save, `F` fit view

Link exports in Manuscript Studio via **Concept Assets** mode (browse to your Concept Studio workspace folder).

## Manuscript Studio

```powershell
dotnet run --project src/ManuscriptStudio

# Generic mode: open a folder of markdown files
dotnet run --project src/ManuscriptStudio -- "D:\path\to\markdown-folder"
```

### Editor controls

- **Wrap** — toggle long-line wrapping in the source editor
- **Light preview** — switch preview between studio dark and GitHub-style light
- **Sync zoom** — keep editor and preview zoom aligned
- **+/− / 100%** — adjust editor zoom; Ctrl+scroll also works on editor and preview
- Settings persist in `settings.json` under `editor`

### Book Authoring mode

1. Switch mode to **Book Authoring** in the toolbar
2. Set **content root** to your publishing workspace (e.g. `D:\repos\books`)
3. Pick series and book — chapter list uses ordered chapters from `content/series/{id}/books/{slug}/chapters/`
4. Use the **right-rail view** combo: **Preview**, **Timeline**, **Relationships**, or **Map**
5. Timeline/Relationships/Map show read-only Mermaid source (no live diagram render in v1)
6. **Insert** menu holds metadata and dialogue helpers; **Debug meta** toggles extended `[!tag]` in preview
7. **Export** menu: PDF (QuestPDF), single view `.mmd`, or all views plus `manifest.json`

View exports are written under:

`{dataRoot}/exports/{seriesId}/{bookId}/views/`

Settings (layout splitters, content root, series/book, last right-rail view) persist under:

- `{AppContext.BaseDirectory}/ManuscriptStudio/settings.json` (preferred)
- `%LocalAppData%\Novolis\ManuscriptStudio\settings.json` (fallback)

### Calypso smoke test

Interactive:

```powershell
dotnet run --project src/ManuscriptStudio
```

- Mode: Book Authoring
- Content root: `D:\repos\books`
- Series: The Calypso Cycle → book: Calypso
- Right-rail view: **Timeline** — Mermaid source includes `2496.099` from `047-marsh-black.md`
- **Relationships** — character nodes from `[!characters]` / `[!pov]` (e.g. Marsh)
- **Map** — system/location nodes (e.g. Calypso)
- **Export all views** writes `.mmd` files and `manifest.json` under app data `exports/`

Headless (no UI):

```powershell
dotnet run --project src/ManuscriptStudio -- --smoke-calypso
```
