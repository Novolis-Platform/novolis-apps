# Documents and settings

← [Documentation index](README.md) · [Sketch JSON](sketchjson.md) · [Getting started](getting-started.md)

## Document actions

| Action | Shortcut | Behavior |
|--------|----------|----------|
| New | `Ctrl+N` | Blank `SketchDocument`; keeps current grid UI props |
| Open | `Ctrl+O` | `.sketchjson` via Avalonia `StorageProvider` |
| Recent | — | Flyout of up to 8 paths from settings |
| Save | `Ctrl+S` | Writes current path, or Save As if untitled |
| Save As | `Ctrl+Shift+S` | New path; appends `.sketchjson` if missing |

Before New / Open / Recent / close, if the document is **dirty**, a `ChoiceDialog` offers **Save** / **Don't Save** / **Cancel**.

## Dirty tracking

- `DocumentChanged` from `SketchControl` sets dirty (unless suppressed during load)
- Title: `Sketch Studio — {name}*` when dirty, else without `*`
- Untitled name is `Untitled` until first Save As

## Load path

Successful Open / Save / Save As call `SketchStudioSettings.RememberDocument`, updating:

- `lastDocumentPath`
- `recentPaths` (MRU, max 8, case-insensitive path compare)

On window **Opened**, if `lastDocumentPath` exists on disk, the file is loaded with dirty suppressed (not re-shuffled through Remember unless you save again).

## Settings file

```text
%LocalAppData%\Novolis\Sketch Studio\settings.json
```

Example:

```json
{
  "lastDocumentPath": "C:\\Users\\…\\sketches\\deck.sketchjson",
  "recentPaths": [
    "C:\\Users\\…\\sketches\\deck.sketchjson",
    "C:\\Users\\…\\sketches\\wire.sketchjson"
  ]
}
```

Corrupt or missing settings start empty; the folder is created on first save.

## What is stored in the sketch file

Grid size / visibility / snap and all elements — see [Sketch JSON](sketchjson.md).  
**Not** in the sketch file: selection, undo stacks, active tool, MRU, window size.

## Serialization step on Save

Before write, the host copies control grid props onto `doc.Grid`, then `SketchJson.Serialize`. That keeps toolbar grid state consistent with the file even if the document object was stale.

## Related

- Format field reference: [sketchjson.md](sketchjson.md) and governance contract
- Exporting pixels/vectors: [Export](export.md)
