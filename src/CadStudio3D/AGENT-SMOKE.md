# Novolis CAD Studio 3D

One app for beginner/intermediate **2D + 3D CAD drafting**, appearance (materials / wall sides), limited mesh modelling, staging, and lit PNG render.

**Agent Surface rule:** every UI mutation goes through the same Cad / Scene session `Execute` catalog the LLM uses. No parallel write paths.

## Command DSL

Cad Studio command bar / `runcommand` accepts AutoCAD-ish scripts (`;`-separated):

```text
Line(Point(0.0,1.0), Point(1.0,1.0)); Circle(Point(2.0,2.0), 0.5); Rect(Point(0,0), Point(4,3)); Extrude(2.4); Material("Concrete");
```

`Point(x,z)` is plan (current Level Y); `Point(x,y,z)` is world. Flat forms still work (`Line(0,1,1,1)`). Unknown verbs forward to the Cad session action catalog when possible (`ExportScene(path)`, `BridgeScene()`, `Studio(model)`, …).

## Ports

| Surface | HTTP | TCP | Purpose |
|---------|------|-----|---------|
| Cad | `:18775` | `:18776` | Draft 2D/3D, appearance, `exportscene` / `bridgescene`, Cad dumps |
| Scene | `:18785` | `:18786` | Mesh, lights, cameras, render/save, Scene dumps |

## LLM script (same as UI)

```text
Cad: new → setstudioworkspace draft2d → addrect → extrudeprofile → setmaterial → exportplanpng
Cad: exportscene / bridgescene
Scene: ensurestudiolights → setactivecamera → matchviewport → saverenderpng → dumpviewport
```

## Build / smoke

```powershell
dotnet build d:\novolis\novolis-apps\src\CadStudio3D -p:NovolisUseProjectReferences=true
dotnet run --project d:\novolis\novolis-apps\src\CadStudio3D -p:NovolisUseProjectReferences=true -- --smoke
dotnet test d:\novolis\novolis-apps\tests\CadStudio3D.Unit -p:NovolisUseProjectReferences=true
```

## Run with agent attach

```powershell
$env:NOVOLIS_CAD_SESSION = "1"
$env:NOVOLIS_SCENE_SESSION = "1"
dotnet run --project d:\novolis\novolis-apps\src\CadStudio3D -p:NovolisUseProjectReferences=true
```

Cad is source of truth (`.cadjson`). Model/Stage workspaces bridge in-memory to `.nov3djson` via `Novolis.Cad.SceneBridge`. Scene→Cad round-trip is out of scope for v1.

Draft Studio remains a Cad-only dogfood host; this app is the product shell.
