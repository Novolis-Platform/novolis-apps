# Mobile UX and feature parity

The Android head uses the same `SketchDocument`, `.sketchjson` format, canvas
tools, layer model, undo history, and PNG/SVG export contract as the wide host.
Only command placement and pointer navigation change.

## Surface

- **Top bar** — document name, dirty state, Save, File, and More.
- **Bottom bar** — Select, Pen, Shapes, Text, Undo, Redo, and More.
- **Tool sheet** — all eleven tools: Pen, Line, Spline, Box, Circle, Speech
  bubble, Text, Text box, Eraser, Paint bucket, and Select.
- **Properties sheet** — Snap, Meetup, Grid, Fill, grid size, stroke width,
  color including alpha, and all stroke styles.
- **Layers sheet** — active layer, add, visibility, and lock.
- **Edit sheet** — Complete, Close shape, Cancel, Fuse, Ungroup, Gridify,
  Undo, Redo, Delete, Paste image, and Clear.
- **Export sheet** — Copy PNG, Save PNG, Copy SVG, and Save SVG.
- **Document sheet** — New, Open, Recent, Save, and Save As.

## Touch contract

- One finger or stylus uses the active tool.
- Two fingers pan and pinch-zoom around the gesture focus.
- Line and Spline expose Complete, Close shape, and Cancel.
- Selection handles remain available; the mobile surface keeps action buttons
  large enough for finger input.
- Physical keyboard users retain the host Ctrl shortcuts.

Android file pickers are handled through `IStorageFile` streams. App-private
working copies provide recovery and the Recent list when a provider exposes a
content URI instead of a local path.

## Validation

```powershell
dotnet build src/SketchStudio/SketchStudio.Android/SketchStudio.Android.csproj -p:NovolisUseProjectReferences=true
dotnet build src/SketchStudio/SketchStudio.csproj -p:NovolisUseProjectReferences=true
```

The Android host should be checked on phone portrait, tablet landscape,
rotation, a stylus, a physical keyboard, and an Android content provider.
