# Shortcuts and tooltips

← [Documentation index](README.md) · [Tools](tools.md) · [UX chrome](ux-chrome.md)

## Discoverability in the app

| Mechanism | Behavior |
|-----------|----------|
| Hover tooltip | Every toolbar control: **title**, **shortcut** (when any), **one-line help**. Show delay ~200 ms (`SketchShortcuts.TipShowDelayMs`). |
| F1 / ? button | Modal **Keyboard shortcuts** dialog — same content as the tables below (`SketchShortcuts.ShowHelpAsync`). |
| Hint strip | Points at hover tips + F1; not a substitute for the full list. |

Source of truth in code: `SketchShortcuts.cs` (File / Tools / Edit / Export / Help rows). Keep this doc aligned when adding chords.

**Chord rule:** letter commands are always **hold Ctrl, then press the letter**. Bare letters are never tools, so typing in a text label, text box, or hex field cannot switch tools. Keys that originate from a `TextBox` are ignored by the window handler (Ctrl+V stays paste-text, not paste-image).

While **Control is held**, every Ctrl-bound toolbar button shows its letter in the corner (`P`, `⇧S`, `↵`, …). Release Control to hide the letters.

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
| Pen / Line / Spline / Box / Circle | `Ctrl+P` `Ctrl+L` `Ctrl+U` `Ctrl+R` `Ctrl+C` |
| Speech bubble / Text / Text box | `Ctrl+B` `Ctrl+T` `Ctrl+X` |
| Eraser / Paint bucket / Select | `Ctrl+E` `Ctrl+K` `Ctrl+M` |

Hold **Ctrl**, then press the letter. **Shift**, **Alt**, or **Meta** together with Ctrl does not switch tools. Spline is `Ctrl+U` because `Ctrl+S` is Save; Select is `Ctrl+M` because `Ctrl+V` pastes an image.

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
- File chords (`Ctrl+N/O/S`, …) and tool chords (`Ctrl+P`, …) are handled on the **window** `KeyDown` and work when the window is focused even if the canvas last received pointer input (after a click on the canvas, control keys still reach the window unless the control marks them handled). File/edit chords win when they share a letter (`Ctrl+S`, `Ctrl+V`, `Ctrl+G`).
- Hold **Ctrl** to reveal the letter badge on each bound button (tunneled `KeyDown` / `KeyUp` on the window).

## See also

- [Editing and canvas](editing.md)
- [Getting started](getting-started.md)
