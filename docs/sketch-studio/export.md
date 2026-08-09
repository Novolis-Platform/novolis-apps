# Export

← [Documentation index](README.md) · [Documents](documents.md) · [Architecture](architecture.md)

Sketch Studio exports **raster and SVG snapshots** of the current document. Export is **host-local** (`SketchExport`) — not part of `.sketchjson`.

## Actions

| Action | Output | Notes |
|--------|--------|-------|
| Copy PNG | Clipboard bitmap + PNG bytes | **Opaque white** background (browser / ChatGPT paste) |
| Save PNG file | `.png` file | Same opaque raster via file picker (disk, not only clipboard) |
| Copy SVG | Clipboard text | Full `<svg xmlns=…>` markup |
| Save SVG file | `.svg` file | Same markup via file picker |

Toolbar icons: image / file-image / code / file-code on **row 3**. Tips documented under [Shortcuts](shortcuts.md). Hidden layers are omitted from export.

## Bounds and padding

Content AABB of all elements (including rotation) plus padding. Empty documents export a 1×1 placeholder (white if opaque PNG).

## PNG details

- Default scale factor `2` (clamped 0.5–8) for sharper clipboard paste
- `opaqueBackground: true` fills white before drawing strokes / text / images
- Uses Avalonia `RenderTargetBitmap` (requires Avalonia platform — see [Smoke](smoke-and-release.md))

## SVG details

- Polyline / polygon paths, text, text boxes, embedded `image` (PNG base64 href)
- Stroke dash arrays from `SketchStrokeStyle`
- Rotation via SVG `transform="rotate(…)"`
- Font family hint: `Segoe UI, sans-serif` for text elements

## What export does not do

- Does not write `.sketchjson` (use Save)
- Does not include the on-screen grid chrome
- Does not round-trip selection or tool state

## Related

- Authoring file: [Documents](documents.md)
- Element model: [Sketch JSON](sketchjson.md)
