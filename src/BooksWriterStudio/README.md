# Books Writer Studio

Avalonia desktop remake of books-writer: series/book/chapter navigation, markdown editing, metadata, diagnostics, PDF/audiobook publish, git checkpoint, and selected-text TTS preview.

## Run

From `novolis-apps`:

```powershell
dotnet run --project src/BooksWriterStudio
dotnet run --project src/BooksWriterStudio -- "D:\path\to\books-repo"
```

Workspace roots are detected via `manuscript.yaml` (NMP/1) or legacy `content/series` / `content/books`.

Local multi-repo iteration: open `Novolis.Platform.slnx` (ProjectReference mode). Released builds restore **nuget.org + GitHub Packages** only.

## Packages

Key consumers: `Novolis.Markup.Manuscript`, `Novolis.Audio.Voice.Manuscript`, `Novolis.Avalonia.Studio` / Controls / Themes, `Novolis.IO.*`.

## Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+S | Save |
| Ctrl+P | Go to chapter |
| Ctrl+Shift+Space | Speak selection |
| F11 | Focus mode |

## Spellcheck

Optional: place `en_US.aff` / `en_US.dic` under `Assets/Dictionaries` (or LocalAppData `Novolis/BooksWriterStudio/Dictionaries`). Use **Check spelling** in the editor toolbar when loaded.

## Related

| App / dogfood | Role |
|---------------|------|
| `BooksMobile` | Mobile reader companion |
| `novolis-dogfooding/apps/manuscript/ManuscriptSmoke` | Manuscript pipeline smoke |
