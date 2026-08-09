# Design

## Purpose

`novolis-apps` hosts shipped desktop applications that consume published Novolis packages from GitHub Packages. Each app under `src/` is self-contained.

## Non-goals

- Publishing NuGet packages from this repository
- In-repo shared libraries or cross-app `ProjectReference`
- Cross-repo `ProjectReference` into sibling `novolis-*` clones

## Books Writer Studio

WinExe for three-column book authoring (chapter nav, markdown editor, metadata/publish/SCM).

- Path: `src/BooksWriterStudio/`

## Draft Studio

WinExe for command-driven 2D/3D CAD-light drafting (LibreCAD/AutoCAD-light): typed DSL (`Line(0,0,1,0)`), mouse tools that emit the same commands, plan-view canvas, Raylib model view, `.cadjson` persistence, and optional `.cadphys.json` export.

- Path: `src/DraftStudio/`
- Data: `%LocalAppData%\Novolis\Draft Studio\default-workspace\draft.cadjson`
- Formats: [`cadjson.md`](../../novolis-governance/docs/cadjson.md)
- Consumes `Novolis.Avalonia.Studio` (`StudioCommandBar`), `Novolis.Commands.Expressions`, `Novolis.Math.Geometry`, `Novolis.Avalonia.Raylib` from GitHub Packages (NuGet-only)

## Sketch Studio

WinExe freehand / whiteboard studio on `SketchControl`.

**Drawing:** pen, line, spline, box, circle, speech bubble, text, text box, eraser, select (rotate grip, Shift multi-select).  
**Options:** grid / snap / meetup / fill / stroke styles / Gridify.  
**Composition:** fuse / ungroup, paste image, undo/redo.  
**Documents:** New/Open/Save/Save As `.sketchjson`; last path + MRU under `%LocalAppData%\Novolis\Sketch Studio\`.  
**Export:** Copy / Save As PNG (opaque) and SVG.  
**Discoverability:** hover tooltips; **F1** shortcut reference.

- Path: `src/SketchStudio/`
- **Docs:** [`docs/sketch-studio/`](sketch-studio/README.md) (getting started, tools, shortcuts, architecture, …)
- Format: [`sketchjson.md`](../../novolis-governance/docs/sketchjson.md)
- Smoke: `dotnet run --project src/SketchStudio -- --smoke`
- Consumes `Novolis.Avalonia.Controls`, `Novolis.Avalonia.Controls.Sketch` from GitHub Packages (NuGet-only)

## Sins of a Capitalism Tycoon

Exe with dual shell for the bounded-minimum economy package:

- Path: `src/SinsOfACapitalismTycoon/`
- `--mode headless` — period loop + console report (agent entrypoint)
- `--mode avalonia` — same report in a desktop window
- Consumes `Novolis.Economy.Core` from GitHub Packages (NuGet-only)

## Capitalist Simulator

Exe dual-shell Capitalism 2 homage (app-local firm/unit/linkage sim + Avalonia UI). Not in the release installer catalog yet.

- Path: `src/CapitalistSimulator/`
- `--mode headless` — month ticks + Spectre report
- `--mode avalonia` — city map / firm interior bridge
- Consumes `Novolis.Avalonia.Studio`, `.Briefing`, `.Controls`, `Novolis.Storage.Json` (NuGet-only)

## Live Studio

Avalonia demo for Novolis Audio Live (DSL editor + visuals), with bundled host and launcher.

- Path: `src/LiveStudio/studio/`

## Package sources

| Source | URL |
|--------|-----|
| GitHub Packages | `Novolis.Avalonia.Studio` and other `Novolis.*` |
| nuget.org | Avalonia, YamlDotNet |

## Related

- [getting-started.md](getting-started.md)
- [release.md](release.md)
- [apps-repos.md](../../novolis-governance/docs/apps-repos.md)
