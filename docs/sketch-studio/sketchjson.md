# Sketch JSON (host notes)

← [Documentation index](README.md) · [Documents](documents.md)

## Authoritative contract

Full wire format (versions, fields, examples, legacy v1):

**[`novolis-governance/docs/sketchjson.md`](../../../novolis-governance/docs/sketchjson.md)**

Implementation: `Novolis.Avalonia.Controls.Sketch` → `SketchJson.Serialize` / `Deserialize`.

This page only covers **how Sketch Studio uses** the format.

## Host responsibilities

| Concern | Host behavior |
|---------|----------------|
| File filter | `*.sketchjson` on Open / Save As |
| Extension | Appends `.sketchjson` if the user omits it |
| Grid sync | Copies `SketchControl` grid props onto `doc.Grid` before serialize |
| Dirty / MRU | Outside the file — [Documents](documents.md) |
| PNG / SVG | Outside the file — [Export](export.md) |

## Features that need version 2+

Speech bubble / text / text box / image / rotation / fuse groups require v2 fields (`kind`, `rotationDegrees`, `groupId`, `text`, `fontSize`, `imagePngBase64`).

## Layers (version 3)

When extra layers exist (or elements leave the default layer), documents write `version: 3` with:

- `layers[]` — `id`, `name`, optional `visible` / `locked`
- `activeLayerId`
- element `layerId` (omitted when default)

Hidden layers are skipped in the canvas and in PNG/SVG export. Locked layers reject paint-bucket / erase / transform.

## Colors

`strokeColor` / `fillColor` accept `#RRGGBB` or `#AARRGGBB` (alpha). Fully transparent: `#00000000`.

## Round-trip checks

- Unit: `Novolis.Avalonia.Unit` / `SketchDocumentTests`
- Host: `--smoke` exercises serialize → deserialize of fused + text + layers + fill — [Smoke and release](smoke-and-release.md)

## Not CAD

Do not confuse with [`.cadjson`](../../../novolis-governance/docs/cadjson.md) (Draft Studio). Sketch is freehand / whiteboard; Draft is analytic CAD-light.
