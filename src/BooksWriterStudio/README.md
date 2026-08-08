# Books Writer Studio

Avalonia **desktop** authoring host: series/book/chapter navigation, markdown editing, metadata, diagnostics, PDF/audiobook publish, git checkpoint, and selected-text TTS preview.

Composes a Wide `AuthoringWorkspace` (`Novolis.Avalonia.Layout`) — nav | primary | context — and calls chapter-aware `Novolis.Manuscript*` libraries in-process (surgery, metrics, ascii, editorial). It is not a second implementation of those ops.

## Run

```powershell
dotnet run --project d:\novolis\novolis-apps\src\BooksWriterStudio
dotnet run --project d:\novolis\novolis-apps\src\BooksWriterStudio -- "D:\path\to\books-repo"
```

Workspace roots are detected via `manuscript.yaml` (NMP/1) or legacy `content/series` / `content/books`.

Local multi-repo iteration: open `d:\novolis\Novolis.Platform.slnx` (ProjectReference mode). Released builds restore **nuget.org + GitHub Packages** only.

## Composition

| Layer | Role in Studio |
|-------|----------------|
| `Novolis.Avalonia.Layout` | Wide `AuthoringWorkspace` shell (nav / primary / context) |
| `Novolis.Avalonia.Controls` / Themes / Studio | Reusable chrome atoms |
| `Novolis.Manuscript*` | Catalog, IO/surgery, metrics, ascii, editorial, export |
| App host | Session, menus, job wiring — thin composition only |

Grain and library-vs-CLI rules:

- `d:\novolis\novolis-governance\docs\avalonia-composition-grain.md`
- `d:\novolis\novolis-governance\docs\library-vs-cli.md`

## Packages

Key consumers: `Novolis.Manuscript`, `Novolis.Manuscript.IO`, `Novolis.Manuscript.Metrics`, `Novolis.Manuscript.Editorial`, `Novolis.Manuscript.Export.*`, `Novolis.Audio.Voice.Manuscript`, `Novolis.Avalonia.Layout` / Studio / Controls / Themes, `Novolis.IO.*`.

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
| `BooksMobile` | Narrow `AuthoringWorkspace` field companion (sync + listen) |
| `novolis-dogfooding/apps/manuscript/ManuscriptSmoke` | Manuscript pipeline smoke |
