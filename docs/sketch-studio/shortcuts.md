# Shortcuts and tooltips

← [Documentation index](README.md) · [Tools](tools.md) · [UX chrome](ux-chrome.md)

## Discoverability in the app

| Mechanism | Behavior |
|-----------|----------|
| Hover tooltip | Every toolbar control: **title**, **shortcut** (when any), **one-line help**. Show delay ~200 ms (`SketchShortcuts.TipShowDelayMs`). |
| F1 / ? button | Modal **Keyboard shortcuts** dialog — same content as the tables below (`SketchShortcuts.ShowHelpAsync`). |
| Hint strip | Points at hover tips + F1; not a substitute for the full list. |

Source of truth in code: `SketchShortcuts.cs` (File / Tools / Edit / Export / Help rows). Keep this doc aligned when adding chords.

## File

| Action | Keys | Notes |
|--------|------|-------|
| New | `Ctrl+N` | Prompts if dirty — [Documents](documents.md) |
| Open | `Ctrl+O` | `.sketchjson` picker |
| Recent | — | Clock flyout; MRU max 8 |
| Save | `Ctrl+S` | Current path or Save As |
| Save As | `Ctrl+Shift+S` | New `.sketchjson` path |
| Shortcuts help | `F1` | Also the ? toolbar button |

## Tools

| Action | Keys |
|--------|------|
| Pen / Line / Spline / Box / Circle | `P` `L` `S` `R` `C` |
| Speech bubble / Text / Text box | `B` `T` `X` |
| Eraser / Paint bucket / Select | `E` `K` `V` |

Letter keys are ignored while **Ctrl** or **Shift** is held so they do not clash with file/edit chords.

## Edit and canvas

| Action | Keys | Where handled |
|--------|------|----------------|
| Undo / Redo | `Ctrl+Z` / `Ctrl+Y` | Host and `SketchControl` |
| Fuse / Ungroup | `Ctrl+G` / `Ctrl+Shift+G` | Host and `SketchControl` |
| Paste image | `Ctrl+V` | Host |
| Select all | `Ctrl+A` | `SketchControl` (Select tool) |
| Complete line/spline | `Enter` | `SketchControl` |
| Close shape | `Ctrl+Enter` (or Shift+Enter) | `SketchControl` |
| Cancel drawing | `Esc` | `SketchControl` |
| Delete selection | `Del` / `Backspace` | `SketchControl` (+ host `Del`) |
| Pan | `Space` + drag | `SketchControl` |

## Export

| Action | Keys |
|--------|------|
| Copy PNG / Save As PNG | — (toolbar) |
| Copy SVG / Save As SVG | — (toolbar) |

See [Export](export.md).

## Focus notes

- Enter / Esc / Space / Ctrl+A need **canvas focus** (`SketchControl`) to fire inside the control.
- File chords (`Ctrl+N/O/S`, …) and tool letters are handled on the **window** `KeyDown` and work when the window is focused even if the canvas last received pointer input (after a click on the canvas, control keys still reach the window unless the control marks them handled).

## See also

- [Editing and canvas](editing.md)
- [Getting started](getting-started.md)
