# Live Studio

Avalonia demo for **Novolis Audio Live** — editable Live DSL, Roslyn interpretation, visualizer windows, and oscillator playback. Split into a **UI studio**, headless **engine host**, and **launcher** that supervises the host process over local IPC.

**Platform:** Windows x64 (studio UI); host and launcher are console apps bundled on publish.

## Projects

| Project | Path | Role |
|---------|------|------|
| `LiveStudio` (studio) | `studio/` | Avalonia UI — editor, presets, visualizers (`Novolis.Audio.Live.Studio.exe`) |
| `LiveStudio.Host` | `host/` | Headless engine — compile/snapshot/swap over LocalIpc |
| `LiveStudio.Launcher` | `launcher/` | Starts host + studio, watchdog, status pipe |
| `LiveStudio.Shared` | `shared/` | Host path resolution, launcher protocol |

Publish of `studio/` bundles `host/` and `launcher/` into the output directory.

## Run (studio only — dev)

From `novolis-apps` (starts studio; session connects to host via launcher when installed/published):

```powershell
dotnet run --project src/LiveStudio/studio
```

Headless smoke (session + host handshake, no UI):

```powershell
dotnet run --project src/LiveStudio/studio -- --headless-demo
```

Run launcher (starts host, optionally spawns studio):

```powershell
dotnet run --project src/LiveStudio/launcher
dotnet run --project src/LiveStudio/launcher -- --host-only
```

Run host standalone:

```powershell
dotnet run --project src/LiveStudio/host
```

## What you should experience

1. **Pulse Bloom** loads into the editor as Live DSL, then compiles and plays
2. Click demos → editor buffer replaced → compile
3. **Ctrl+Space** Live DSL completion; **F5** / **Ctrl+Enter** compile
4. Child windows: **Graph**, **Piano roll**, **Interpretation**
5. Beat / Bar / Phrase ticks (audio-driven clock)

## Key Novolis packages

| Package | Role |
|---------|------|
| `Novolis.Avalonia.Live` | Editor, compiler UI, completion, visualizers |
| `Novolis.Audio.Live` | Live DSL session and program model |
| `Novolis.Audio.Live.Protocol` | IPC DTOs (compile, snapshot, queue swap) |
| `Novolis.Audio.Live.Visuals` | Graph / piano-roll windows |
| `Novolis.Transports.LocalIpc` | Studio ↔ host transport |

Studio wiring (workspace, session, presets) lives in this app; editor/compiler/visualizers come from `Novolis.Avalonia.Live`.

## Local development (ProjectReference mode)

```powershell
dotnet build src/LiveStudio/studio -p:NovolisUseProjectReferences=true
```

For cross-repo Audio/Live work, use **`Novolis.Platform.slnx`**. Committed builds are **NuGet-only** (`2026.1.*`).

## Releases

Published on merge to `main` as `LiveStudioSetup-{version}-win-x64.exe` and `Novolis.Audio.Live.Studio-{version}-win-x64.zip`. See [novolis-apps release catalog](../../README.md#releases).
