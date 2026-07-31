# Sketch Studio

Freehand sketching desk built on [`Novolis.Avalonia.Controls`](../../../novolis-avalonia/src/Novolis.Avalonia.Controls/README.md) `SketchControl`.

Tools: Pen, Line, Spline, Box, Circle, Eraser, Select  
Options: Snap to grid, Meetup (vertex snap), Gridify, stroke color + width + style  
Documents: Open / Save / Save As as `.sketchjson`  
Clipboard: Copy PNG (opaque), Copy SVG

## Install / run

From `novolis-apps`:

```powershell
dotnet run --project src/SketchStudio
```

**Dependencies:** `Novolis.Avalonia.Controls`, Avalonia Desktop, Inno packaging.

## Quick start

The app hosts a `SketchControl` with full toolbar chrome. For programmatic use of the same APIs:

```csharp
using Novolis.Avalonia.Controls;

var sketch = new SketchControl
{
    Tool = SketchTool.Pen,
    GridSize = 20,
    GridVisible = true,
    SnapEnabled = true,
    MeetupEnabled = true,
    StrokeColor = Colors.Black,
    StrokeWidth = 2,
    StrokeStyle = SketchStrokeStyle.Solid,
};
sketch.GridifySelection(); // snap selected strokes to grid
var json = SketchJson.Serialize(sketch.Document!);
```

## API (consumed from Controls)

| API | Purpose |
|-----|---------|
| `SketchControl` | Main canvas: Pen/Line/Spline/Rect/Ellipse/Eraser/Select |
| `SketchControl.Document` / `Tool` / `GridSize` / `SnapEnabled` / `MeetupEnabled` | Document and tool state |
| `SketchControl.CompleteDrawing` / `GridifySelection` / `Undo` / `Redo` / `Clear` | Editing |
| `SketchControl.DocumentChanged` / `SelectionChanged` | Change notifications |
| `SketchTool` | Pen, Select, Line, Spline, Rect, Ellipse, Eraser |
| `SketchStrokeStyle` | Solid, Dashed, Dotted, DashDot, Stipple |
| `SketchJson.Serialize` / `Deserialize` | `.sketchjson` persistence |
| `SketchDocument` | Strokes, selection, grid, undo history |

SketchStudio adds internal `SketchExport.ToSvg` / `ToPng` for clipboard export (white background for opaque PNG).

## Shortcuts

`Ctrl+N/O/S`, `Ctrl+Shift+S`, `Ctrl+Z/Y`, tool keys `P/L/S/R/C/E/V`, `Del` deletes selection.

## Related / dogfood

| App | Notes |
|-----|-------|
| [SketchLab](../../../novolis-dogfooding/apps/avalonia/SketchLab) | Lighter dogfood variant; same `SketchControl` + PNG/SVG clipboard |
| `Novolis.Avalonia.Unit` | `SketchJson` round-trip tests |
