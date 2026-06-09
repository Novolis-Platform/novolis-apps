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
4. Use metadata and dialogue toolbar helpers; **Export PDF** writes QuestPDF output

Settings (layout splitters, content root, series/book) persist under:

- `{AppContext.BaseDirectory}/ManuscriptStudio/settings.json` (preferred)
- `%LocalAppData%\Novolis\ManuscriptStudio\settings.json` (fallback)

### Calypso smoke test

```powershell
dotnet run --project src/ManuscriptStudio
```

- Mode: Book Authoring
- Content root: `D:\repos\books`
- Series: The Calypso Cycle → book: Calypso
- Open chapter `047-marsh-black.md`
- Confirm `[!date]` metadata in editor; toggle **Debug meta** for extended tags
- Save (Ctrl+S) preserves UTF-8
