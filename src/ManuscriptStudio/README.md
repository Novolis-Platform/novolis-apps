# Manuscript Studio

Avalonia editor for hand-crafted markdown with built-in modes:

- **Generic Markdown** — open any folder, browse `.md` files, GFM preview
- **Book Authoring** — series/book/chapter navigation for a publishing workspace layout (see `content/series/` under your content root)

## Editor and preview

- Center editor: `Novolis.Avalonia.Markdown` (`MarkdownSourceEditor` — line numbers, word wrap, Ctrl+scroll zoom)
- Generic mode preview: `MarkdownPreviewPane` on the right rail (independent Ctrl+scroll zoom)

## Preview and metadata views

- Generic mode: live GFM preview via `Novolis.Avalonia.Markdown`
- Book Authoring: Markdig HTML preview plus metadata-driven Mermaid **source** views:
  - **Timeline** — chapter dates from `[!date]` metadata
  - **Relationships** — character co-occurrence from `[!characters]` and `[!pov]`
  - **Map** — `[!system]` / `[!location]` hierarchy with chapter pins

v1 shows Mermaid as read-only source text with Copy / Save `.mmd` in the right rail. Live diagram rendering is not included yet.

## Export

- **PDF** — QuestPDF layout (Community license) to `{dataRoot}/exports/` or a folder you pick
- **Mermaid views** — `{dataRoot}/exports/{seriesId}/{bookId}/views/{timeline|relationships|map}.mmd` plus optional `manifest.json` when exporting all views

## Settings

Stored at `{AppContext.BaseDirectory}/ManuscriptStudio/settings.json`, with fallback to `%LocalAppData%/Novolis/ManuscriptStudio/settings.json`.

Includes layout column widths, content root, active mode, last series/book selection, and Book Authoring right-rail view.

## Run

```powershell
dotnet run --project src/ManuscriptStudio
dotnet run --project src/ManuscriptStudio -- "D:\path\to\folder"
dotnet run --project src/ManuscriptStudio -- --smoke-calypso
```
