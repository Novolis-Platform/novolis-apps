# UX chrome

← [Documentation index](README.md) · [Shortcuts](shortcuts.md) · [Tools](tools.md)

## Layout

```text
┌─ Row 1 ─────────────────────────────────────────────────────────┐
│ File (New Open Recent Save SaveAs) │ Tools… (incl. paint bucket) │
├─ Row 2 ─────────────────────────────────────────────────────────┤
│ Snap Meetup Grid Fill │ Grid │ Width │ Color swatches             │
├─ Row 3 ─────────────────────────────────────────────────────────┤
│ Stroke styles │ Layers (+ / eye / lock) │ Edit │ Export files     │
├─ Hint strip ────────────────────────────────────────────────────┤
│ Hover tips · F1 · K bucket · Save PNG/SVG files · …              │
├─────────────────────────────────────────────────────────────────┤
│                     SketchControl (canvas)                       │
├─ Status bar (bottom) ───────────────────────────────────────────┤
│ Tool · style · counts · color · width · messages                 │
└─────────────────────────────────────────────────────────────────┘
```

Default window ~1180×760; minimum 720×480. Code-built UI (no XAML). Theme: Avalonia Fluent **Light** + Inter.

## File cluster

New, Open, **Recent** (clock + `MenuFlyout`), Save, Save As — Font Awesome via Optris.

## Tool cluster

Eleven tool toggles (exclusive) including **paint bucket**, plus Complete and Close shape. See [Tools](tools.md).

## Options and colors

Snap / Meetup / Grid / Fill, grid size, stroke width, and an expanded palette (opaque + alpha + transparent).

## Layers and actions (row 3)

Layer combo + add / visibility / lock; fuse / ungroup / gridify / undo / redo / clear / paste; **Copy PNG**, **Save PNG file**, **Copy SVG**, **Save SVG file**, shortcuts (?).

## Status bar

Docked to the **bottom** of the window (not the toolbar). Default: `{Tool} · {StrokeStyle} · {elementCount} · sel {n} · {color} · w{width}` plus ` · fill` when Fill is on. Many commands replace it with a short success or error string.

## Title bar

`Sketch Studio — {file or Untitled}` with trailing `*` when dirty — [Documents](documents.md).

## Tooltip contract

`SketchShortcuts.ApplyTip` sets tip text and a short show delay on every chrome control. Format:

```text
{Title}  ({Shortcut})
{One-line detail}
```

## F1 dialog

Scrollable window listing File / Tools / Edit / Export / Help rows. **Esc** or **F1** closes. Keep content in sync with [shortcuts.md](shortcuts.md) and `SketchShortcuts.cs`.

## Accessibility / density notes

- Three toolbar rows reduce horizontal crowding; status stays readable on the bottom bar
- Icon-only chrome relies on tooltips — do not remove tips when rearranging the toolbar
- Clear has no confirm (unlike dirty close) — tip text states it is undoable
