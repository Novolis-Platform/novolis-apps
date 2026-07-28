# Books Writer Studio

Avalonia desktop remake of books-writer: series/book/chapter navigation, markdown editing, metadata, diagnostics, PDF/audiobook publish, git checkpoint, and selected-text TTS preview.

## Run

```powershell
dotnet run --project src/BooksWriterStudio
dotnet run --project src/BooksWriterStudio -- "D:\path\to\books-repo"
```

Workspace roots are detected via `content/series` or `content/books`.

## Packages

Consumes published Novolis packages only (GitHub Packages + nuget.org): `Novolis.Markup.Manuscript`, `Novolis.Audio.Voice.Manuscript`, `Novolis.Avalonia.*`, `Novolis.IO.*`.

## Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+S | Save |
| Ctrl+P | Go to chapter |
| Ctrl+Shift+Space | Speak selection |
| F11 | Focus mode |

## Spellcheck

Optional: place `en_US.aff` / `en_US.dic` under `Assets/Dictionaries` (or LocalAppData `Novolis/BooksWriterStudio/Dictionaries`). Use **Check spelling** in the editor toolbar when loaded.
