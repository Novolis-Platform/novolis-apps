# Sketch Studio — documentation

Freehand / whiteboard product host on `Novolis.Avalonia.Controls.Sketch`.
Installers and portable zips ship from `novolis-apps` releases.

## Start here

1. [Getting started](getting-started.md) — install, run, first five minutes, smoke
2. [Tools](tools.md) — Pen through Select; how each tool behaves
3. [Shortcuts and tooltips](shortcuts.md) — F1, hover tips, full key map
4. [Editing and canvas](editing.md) — snap, meetup, fuse, rotate, pan, undo

## Documents and export

5. [Documents and settings](documents.md) — `.sketchjson` lifecycle, dirty close, MRU
6. [Export](export.md) — Copy / Save As PNG and SVG
7. [Sketch JSON](sketchjson.md) — host notes; full wire contract in governance

## Contributors

8. [Architecture](architecture.md) — host vs Controls.Sketch, packages, layers
9. [Smoke and release](smoke-and-release.md) — `--smoke`, Inno, catalog assets
10. [UX chrome](ux-chrome.md) — toolbar layout, status line, discoverability

## External contracts

| Doc | Location |
|-----|----------|
| Wire format (authoritative) | [`novolis-governance/docs/sketchjson.md`](../../../novolis-governance/docs/sketchjson.md) |
| Package README | [`Novolis.Avalonia.Controls.Sketch`](../../../novolis-avalonia/src/Novolis.Avalonia.Controls.Sketch/README.md) |
| Apps design map | [`design.md`](../design.md) |
| Apps install / release | [`getting-started.md`](../getting-started.md) · [`release.md`](../release.md) |
| Product source | [`src/SketchStudio`](../../src/SketchStudio/) |
| Dogfood twin | [SketchLab](../../../novolis-dogfooding/apps/avalonia/SketchLab) |

## Quick run

```powershell
dotnet run --project d:\novolis\novolis-apps\src\SketchStudio -p:NovolisUseProjectReferences=true
dotnet run --project d:\novolis\novolis-apps\src\SketchStudio -p:NovolisUseProjectReferences=true -- --smoke
```

In the app: hover any control for a tip; press **F1** for the shortcut dialog.
