# Draft Studio

Windows desktop **CAD-light** editor: command-driven 2D drafting and 3D extrusion on `.cadjson` documents, with Raylib model preview and phys export. Built on the shared Avalonia CAD stack with full **agent parity** (UI and LLM share `CadSessionService.Execute`).

**Platform:** Windows x64 (Avalonia + Raylib).

## Run

From `novolis-apps`:

```powershell
dotnet run --project src/DraftStudio
```

Headless smoke:

```powershell
dotnet run --project src/DraftStudio -- --smoke
```

With agent attach:

```powershell
$env:NOVOLIS_AVALONIA_AGENT = "1"
dotnet run --project src/DraftStudio
```

## Local development (ProjectReference mode)

```powershell
dotnet build src/DraftStudio -p:NovolisUseProjectReferences=true
dotnet test tests/DraftStudio.Unit -p:NovolisUseProjectReferences=true
```

Committed builds use **NuGet-only** (`Novolis.*` `2026.1.*` from GitHub Packages). For sibling package work, use **`Novolis.Platform.slnx`** — do not add local NuGet folder feeds.

## Key Novolis packages

| Package | Role |
|---------|------|
| `Novolis.Avalonia.Cad` | Editor surface, tools, command bus, `.cadjson` |
| `Novolis.Cad.Primitives` | Document DTOs and phys export |
| `Novolis.Avalonia.Studio` | Command bar, tool strip, status feedback |
| `Novolis.Avalonia.Raylib` | 3D model viewport (presented-frame PNG dumps) |
| `Novolis.Avalonia.Agent` | LLM/MCP agent host (`CadSessionSurface`) |
| `Novolis.Commands.Expressions` | Command DSL evaluation |
| `Novolis.Math.Geometry` | Plan geometry |

## What to try

1. Command bar: `Level(3)` → `Rect(0,0,8,6)` → `Box(2,1,3)` → `Save`
2. Toggle elevation, continuous line, and shape tools in the chrome
3. **Dump** writes artifact JSON under `%LocalAppData%\Novolis\Draft Studio\dumps\`

Agent smoke walkthrough: [AGENT-SMOKE.md](AGENT-SMOKE.md).

## Releases

Published on merge to `main` as `DraftStudioSetup-{version}-win-x64.exe` and portable zip. See [novolis-apps release catalog](../../README.md#releases).
