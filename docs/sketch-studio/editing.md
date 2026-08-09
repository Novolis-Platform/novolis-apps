# Editing and canvas

← [Documentation index](README.md) · [Tools](tools.md) · [Shortcuts](shortcuts.md)

## Grid, snap, meetup

| Control | Effect |
|---------|--------|
| **Grid** | Show / hide background grid. Persisted as `grid.visible`. |
| **Grid size** | Spacing 5–80. Persisted as `grid.size`. |
| **Snap to grid** | Quantize pointer samples while drawing. Persisted as `grid.snapEnabled`. |
| **Meetup** | Snap endpoints to nearby vertices for Line / Spline / shape tools. **Disabled mid-stroke for Pen** so freehand is not pulled to existing geometry. |
| **Gridify** | Snap the **current selection** onto the grid (undoable). |

## Fill

When **Fill** is on, new closed shapes use the active color as `fillColor`. Color swatches update stroke and, if Fill is on, fill together.

## Selection transforms

With **Select (`V`)**:

- Drag the body to move
- Corner / edge handles resize
- Grip **above** the selection rotates (`rotationDegrees` in JSON)
- **Shift** adds to selection / marquee
- **Ctrl+A** selects every element (canvas focus)
- **Del** / **Backspace** deletes the selection

## Fuse and ungroup

| Action | Shortcut | Model |
|--------|----------|-------|
| Fuse | `Ctrl+G` | ≥2 selected elements share a new `groupId` |
| Ungroup | `Ctrl+Shift+G` | Clears `groupId` |

Selecting any group member expands to the whole group for move / resize / rotate / delete. This is a **group**, not a boolean union of geometry.

## Images

**Ctrl+V** (or Paste toolbar) inserts a clipboard bitmap as an `image` element at the viewport center (`PasteImage`). Placement rect is stored in `points`; pixels in `imagePngBase64`. See [Sketch JSON](sketchjson.md) and [Export](export.md).

## Undo / redo / clear

| Action | Shortcut | Notes |
|--------|----------|-------|
| Undo | `Ctrl+Z` | Document history |
| Redo | `Ctrl+Y` | |
| Clear | — | Removes all elements; undoable; no confirm dialog |

## Pan

Hold **Space** and drag to pan the canvas (`SketchControl`). Release Space to return to the active tool.

## Cancel in-progress drawing

**Esc** discards an unfinished Line / Spline / drag. Toolbar Complete / Close only apply when a draft exists.

## Related

- Persistence of grid + elements: [Documents](documents.md)
- Opaque PNG for chat paste: [Export](export.md)
