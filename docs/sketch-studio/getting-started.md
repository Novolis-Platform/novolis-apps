# Getting started

← [Documentation index](README.md)

## Prerequisites

- .NET SDK 10 (`novolis-apps` `global.json`)
- GitHub Packages auth for `Novolis.*` restore (see apps [getting-started](../getting-started.md))
- Windows desktop for Avalonia (installer is win-x64)

## Install (released build)

From [novolis-apps Releases](https://github.com/Novolis-Platform/novolis-apps/releases):

| Asset | Use |
|-------|-----|
| `SketchStudioSetup-*-win-x64.exe` | Inno installer → `%LOCALAPPDATA%\Programs\Novolis\Sketch Studio` |
| `SketchStudio-*-win-x64.zip` | Portable folder |
| `SHA256SUMS.txt` | Verify before running |

See apps [getting-started](../getting-started.md) for SmartScreen / hash notes.

## Run from source

```powershell
dotnet run --project d:\novolis\novolis-apps\src\SketchStudio
```

Sibling library iteration (ProjectReference mode — no local NuGet feed):

```powershell
dotnet run --project d:\novolis\novolis-apps\src\SketchStudio -p:NovolisUseProjectReferences=true
```

Headless check:

```powershell
dotnet run --project d:\novolis\novolis-apps\src\SketchStudio -p:NovolisUseProjectReferences=true -- --smoke
```

Details: [Smoke and release](smoke-and-release.md).

## First five minutes

1. **Pen (`P`)** — drag freehand. Meetup snap stays off mid-stroke so lines are not yanked to nearby vertices.
2. **Line (`L`)** — click vertices; **Enter** finishes; **Ctrl+Enter** closes (≥3 points) or click the start vertex; **Esc** cancels.
3. **Select (`V`)** — move, resize handles, rotate grip above the selection; **Shift** multi-select.
4. **Ctrl+S** — save `.sketchjson`. Use **Copy PNG** or **Save As PNG** for opaque raster export.
5. **F1** — open the in-app shortcut reference (same tables as [shortcuts](shortcuts.md)).

Next launch reopens the last saved/opened path when the file still exists. Use the **Recent** (clock) button for the MRU list.

## Where things live

| Item | Path |
|------|------|
| Settings / MRU | `%LocalAppData%\Novolis\Sketch Studio\settings.json` |
| Installed app | `%LocalAppData%\Programs\Novolis\Sketch Studio` |
| Document format | [Sketch JSON](sketchjson.md) → governance wire contract |

## Next reads

- [Tools](tools.md) — every drawing tool
- [Documents and settings](documents.md) — dirty close, Save As, Recent
- [Export](export.md) — PNG/SVG clipboard and files
