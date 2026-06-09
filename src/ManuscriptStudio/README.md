# Manuscript Studio

Avalonia editor for hand-crafted markdown with built-in modes:

- **Generic Markdown** — open any folder, browse `.md` files, GFM preview
- **Book Authoring** — series/book/chapter navigation for a publishing workspace layout (see `content/series/` under your content root)

## Preview

- Generic mode: Markdig + Avalonia.HtmlRenderer
- Book Authoring mode: books-aligned metadata handling + Markdig HTML

## PDF export

Book Authoring mode exports a simplified QuestPDF layout (Community license) to `{dataRoot}/exports/` or a folder you pick.

## Settings

Stored at `{AppContext.BaseDirectory}/ManuscriptStudio/settings.json`, with fallback to `%LocalAppData%/Novolis/ManuscriptStudio/settings.json`.

Includes layout column widths, content root, active mode, and last series/book selection.

## Run

```powershell
dotnet run --project src/ManuscriptStudio
dotnet run --project src/ManuscriptStudio -- "D:\path\to\folder"
```
