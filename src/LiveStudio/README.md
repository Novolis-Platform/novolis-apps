# LiveStudio

Avalonia demo for **Novolis Audio Live** — editable Live DSL demos, Roslyn interpretation, visualizer windows, and oscillator playback.

## Run

```bash
dotnet run --project novolis-apps/src/LiveStudio/studio/LiveStudio.csproj
```

Headless smoke:

```bash
dotnet run --project novolis-apps/src/LiveStudio/studio/LiveStudio.csproj -- --headless-demo
```

## What you should experience

1. **Pulse Bloom** loads **into the editor** as real Live DSL, then compiles and plays
2. Click demos → editor buffer replaced → compile
3. **Ctrl+Space** Live DSL completion; **F5** / **Ctrl+Enter** compile
4. Child windows: **Graph**, **Piano roll**, **Interpretation** (code → structure)
5. Beat / Bar / Phrase ticks (audio-driven clock)

## Library dependency

Editor, compiler, completion, and visualizers come from `Novolis.Avalonia.Live`. This app keeps workspace wiring, session, and showcase preset adapters.
