# Manuscript Studio

Avalonia editor for hand-crafted markdown with built-in modes:

- **Generic Markdown** — open any folder, browse `.md` files, GFM preview
- **Book Authoring** — series/book/chapter navigation for a publishing workspace layout (see `content/series/` under your content root)

## Editor and preview

`MainWindow` hosts a single `MarkdownAuthoringWorkspace` with three resizable columns. Each column is an `AuthoringPane` (action bar + body):

| Pane | Action bar | Body |
|------|------------|------|
| Navigation (left) | Mode-specific browse/open actions | Tree or chapter list |
| Editor (center) | Save, wrap, zoom, dirty indicator + mode tools | AvaloniaEdit markdown source |
| Preview (right) | Light theme, sync zoom, preview zoom + view picker | HTML or Mermaid views |

Column widths persist in settings. Editor and preview share zoom options (with optional sync).

Preview uses themed HTML with readable typography, wrapped tables/code, and metadata styling in Book Authoring mode.

## Preview and metadata views

- Generic mode: live GFM preview (Markdig + studio CSS)
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

Includes layout column widths, editor wrap/zoom/theme/sync settings, content root, active mode, last series/book selection, and Book Authoring right-rail view.

## Run

```powershell
dotnet run --project src/ManuscriptStudio
dotnet run --project src/ManuscriptStudio -- "D:\path\to\folder"
dotnet run --project src/ManuscriptStudio -- --smoke-calypso
```
