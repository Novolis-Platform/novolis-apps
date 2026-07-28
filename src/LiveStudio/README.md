# LiveStudio

Avalonia demo for **Novolis Audio Live** — typed live coding, queued swaps, and oscillator playback.

## Run the demo

```bash
dotnet run --project novolis-apps/src/LiveStudio/studio/LiveStudio.csproj
```

The studio starts its own host if the launcher isn’t running. Prefer the launcher when you want supervised restarts:

```bash
dotnet run --project novolis-apps/src/LiveStudio/launcher/LiveStudio.Launcher.csproj
```

Headless smoke (no UI):

```bash
dotnet run --project novolis-apps/src/LiveStudio/studio/LiveStudio.csproj -- --headless-demo
```

You should hear **Pulse Bloom** within a few seconds. Transport Beat/Bar/Phrase should tick continuously (audio-driven clock).

## What you should see / hear

Within a few seconds of connect:

1. **Pulse Bloom** auto-compiles and plays (lead / bass / kick)
2. Transport shows **Beat · Bar · Phrase** ticking with a beat pulse
3. After ~8s each, demo continues to **Signal Drift** then **Phrase Lift**
4. Click any preset to swap immediately (cancels the auto demo)
5. Edit `Note.Play(C4)` → `Note.Play(E5)`, press **F5** or **Ctrl+Enter**

## Tips

| Action | How |
|--------|-----|
| Replay the 3-preset demo | **Replay demo** |
| Live swap policy | Toolbar **Swap** combo |
| Reset the REPL buffer | **Reset buffer** |

v0 render maps instruments to basic waveforms; effect names on the graph are labels only.
