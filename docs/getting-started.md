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

## Manuscript Studio

```powershell
dotnet run --project src/ManuscriptStudio

# Generic mode: open a folder of markdown files
dotnet run --project src/ManuscriptStudio -- "D:\path\to\markdown-folder"
```

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
