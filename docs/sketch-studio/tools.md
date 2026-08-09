# Tools

← [Documentation index](README.md) · [Shortcuts](shortcuts.md) · [Editing](editing.md)

All tools live on `SketchControl` (`SketchTool`). The host toolbar buttons and letter keys select the same enum.

| Tool | Key | Interaction |
|------|-----|-------------|
| Pen | `P` | Drag freehand. Grid snap may apply; **meetup is off** while dragging. |
| Line | `L` | Click vertices. **Enter** commits open polyline; **Ctrl+Enter** closes (≥3 points) or click start; **Esc** cancels. |
| Spline | `S` | Click control points; Catmull-Rom tessellation on commit. Same Enter / Ctrl+Enter / Esc as Line. |
| Box | `R` | Drag an axis-aligned rectangle. |
| Circle | `C` | Drag an ellipse; hold **Shift** for a true circle. |
| Speech bubble | `B` | Drag a rounded body with a triangular tail. |
| Text | `T` | Click to place a label; edit in place. |
| Text box | `X` | Drag a bordered box; edit text inside. |
| Eraser | `E` | Click or drag over strokes to erase them. |
| Paint bucket | `K` | Fill a **closed** shape under the cursor, or **flood-fill** an enclosed pocket between strokes (does not auto-close open freehand into a blob). |
| Select | `V` | Click select; **Shift** multi-select / marquee. Move, resize, rotate. **Ctrl+A** selects all (canvas focus). |

## Complete / close chrome

Toolbar check and polygon icons call `CompleteDrawing(closeShape: false|true)` — same as Enter / Ctrl+Enter when the canvas owns focus.

## Stroke appearance

While a drawing tool is active, new strokes use:

- Expanded color swatches (opaque, translucent `#AARRGGBB`, fully transparent `#00000000`) — [UX chrome](ux-chrome.md)
- Width slider (0.25–16)
- Style toggles: Solid, Dashed, Dotted, Dash-dot, Stipple
- **Fill** checkbox — new closed shapes get the current color as fill
- **Paint bucket** — recolor a closed shape, or flood an enclosed empty region bounded by strokes
- **Colors** — 13 opaque swatches + **Custom…** (hex / RGBA, including alpha)

Persisted on each element in `.sketchjson` (`strokeColor`, `strokeWidth`, `strokeStyle`, `fillColor`, `closed`). See [Sketch JSON](sketchjson.md).

## Layers

Toolbar row 3: active layer combo, **+** add, eye (visibility), lock.

- New shapes are created on the **active** layer (`layerId` in JSON; format version 3 when non-default layers exist)
- Hidden layers are not drawn, hit-tested, or exported
- Locked layers cannot be moved, erased, or paint-bucket filled

## Related canvas behaviors

Documented in [Editing and canvas](editing.md): Snap, Meetup, Grid, Gridify, Fuse/Ungroup, pan (`Space`+drag), rotation grip.
