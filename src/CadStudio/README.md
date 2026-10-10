# Novolis CAD Studio

Windows desktop app for beginner/intermediate **2D + 3D CAD drafting** and explicit **Ship** authoring: appearance (materials / wall sides), limited mesh modelling, staging, and lit PNG render. Dual **Cad** and **Scene** agent surfaces share the same session `Execute` catalog the UI and LLM use — no parallel write paths.

**Platform:** Windows x64 (Avalonia + WGL + Raylib).

## Run

```powershell
dotnet run --project d:\novolis\novolis-apps\src\CadStudio\CadStudio.csproj
```

Headless smoke (no UI):

```powershell
dotnet run --project d:\novolis\novolis-apps\src\CadStudio\CadStudio.csproj -- --smoke
```

With agent attach (ports bind only when these are set):

```powershell
$env:NOVOLIS_CAD_SESSION = "1"
$env:NOVOLIS_SCENE_SESSION = "1"
dotnet run --project d:\novolis\novolis-apps\src\CadStudio\CadStudio.csproj
```

## Local development (ProjectReference mode)

Cross-repo iteration on Cad/3D packages: open **`Novolis.Platform.slnx`** or build with ProjectReference mode:

```powershell
dotnet build d:\novolis\novolis-apps\src\CadStudio\CadStudio.csproj -p:NovolisUseProjectReferences=true
dotnet test d:\novolis\novolis-apps\tests\CadStudio.Unit\CadStudio.Unit.csproj -p:NovolisUseProjectReferences=true
```

Committed builds use **NuGet-only** (`Novolis.*` `2026.1.*` from GitHub Packages + nuget.org). No local folder feeds.

## Key Novolis packages

| Package | Role |
|---------|------|
| `Novolis.Avalonia.Cad` | Plan viewport, command DSL, `.cadjson` session |
| `Novolis.Avalonia.Ship.Design` | Ship PLAN / MODEL / ANALYZE mode and `.shipjson` session |
| `Novolis.Avalonia.Modeling` | Scene editor, mesh/lights/cameras |
| `Novolis.Cad.SceneBridge` | `exportscene` / `bridgescene` → `.nov3djson` |
| `Novolis.Avalonia.Studio` | Command bar, workspace chrome, feedback |
| `Novolis.Avalonia.Agent` | LLM/MCP agent host |
| `Novolis.Avalonia.Raylib` | 3D model viewport |

## Agent surfaces

Cad and Scene transports attach only when `NOVOLIS_CAD_SESSION` / `NOVOLIS_SCENE_SESSION` are set.

| Surface | HTTP | TCP | Purpose |
|---------|------|-----|---------|
| Cad | `:18775` | `:18776` | Draft 2D/3D, appearance, export/bridge |
| Scene | `:18785` | `:18786` | Mesh, lights, cameras, render/save |

Command bar accepts AutoCAD-ish scripts (`;`-separated), e.g. `Line(Point(0,1), Point(1,1)); Extrude(2.4); Material("Concrete");`. Ship mode keeps `.shipjson` authoritative and uses `.cadjson` only as an explicit projection/import/export bridge.

Agent smoke details: [AGENT-SMOKE.md](AGENT-SMOKE.md).

## Data migration

On first launch, the product consolidates the former `Draft Studio`, `CAD
Studio 3D`, and `Ship Designer` data roots into `migrations`. Copies never
overwrite existing files, delete source data, or convert `.shipjson` to
`.cadjson`; the original directory structure and document formats remain
available for explicit opening.

## Releases

Published on merge to `main` as `CadStudioSetup-{version}-win-x64.exe` and portable zip. See [novolis-apps release catalog](../../README.md#releases).
