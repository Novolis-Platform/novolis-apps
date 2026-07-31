# Novolis CAD Studio 3D

Windows desktop app for beginner/intermediate **2D + 3D CAD drafting**: appearance (materials / wall sides), limited mesh modelling, staging, and lit PNG render. Dual **Cad** and **Scene** agent surfaces share the same session `Execute` catalog the UI and LLM use — no parallel write paths.

**Platform:** Windows x64 (Avalonia + WGL + Raylib/Vulkan backends).

## Run

From `novolis-apps`:

```powershell
dotnet run --project src/CadStudio3D
```

Headless smoke (no UI):

```powershell
dotnet run --project src/CadStudio3D -- --smoke
```

With agent attach (Cursor MCP / `Novolis.Avalonia.Agent`):

```powershell
$env:NOVOLIS_AVALONIA_AGENT = "1"
dotnet run --project src/CadStudio3D
```

## Local development (ProjectReference mode)

Cross-repo iteration on Cad/3D packages: open **`Novolis.Platform.slnx`** or build with ProjectReference mode:

```powershell
dotnet build src/CadStudio3D -p:NovolisUseProjectReferences=true
dotnet test tests/CadStudio3D.Unit -p:NovolisUseProjectReferences=true
```

Committed builds use **NuGet-only** (`Novolis.*` `2026.1.*` from GitHub Packages + nuget.org). No local folder feeds.

## Key Novolis packages

| Package | Role |
|---------|------|
| `Novolis.Avalonia.Cad` | Plan viewport, command DSL, `.cadjson` session |
| `Novolis.Avalonia.3D` | Scene editor, mesh/lights/cameras |
| `Novolis.Cad.SceneBridge` | `exportscene` / `bridgescene` → `.nov3djson` |
| `Novolis.Avalonia.Studio` | Command bar, workspace chrome, feedback |
| `Novolis.Avalonia.Agent` | LLM/MCP agent host |
| `Novolis.Avalonia.Raylib` | 3D model viewport |
| `Novolis.Rendering.Backends.Vulkan` | Lit render pipeline |

## Agent surfaces

| Surface | HTTP | TCP | Purpose |
|---------|------|-----|---------|
| Cad | `:18775` | `:18776` | Draft 2D/3D, appearance, export/bridge |
| Scene | `:18785` | `:18786` | Mesh, lights, cameras, render/save |

Command bar accepts AutoCAD-ish scripts (`;`-separated), e.g. `Line(Point(0,1), Point(1,1)); Extrude(2.4); Material("Concrete");`.

Agent smoke details: [AGENT-SMOKE.md](AGENT-SMOKE.md).

## Releases

Published on merge to `main` as `CadStudio3DSetup-{version}-win-x64.exe` and portable zip. See [novolis-apps release catalog](../../README.md#releases).
