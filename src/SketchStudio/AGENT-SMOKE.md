# Sketch Studio agent smoke

Wire-up uses `Novolis.Avalonia.Agent` (+ Protocol). Cursor MCP `avalonia-agent` is preferred when loaded; otherwise use `--agent-probe`.

## Build

```powershell
dotnet build d:\novolis\novolis-apps\src\SketchStudio -p:NovolisUseProjectReferences=true
```

## Run Sketch Studio with agent host

```powershell
$env:NOVOLIS_AVALONIA_AGENT = "1"
dotnet run --project d:\novolis\novolis-apps\src\SketchStudio -p:NovolisUseProjectReferences=true
```

Pipe: `novolis-avalonia-agent-sketch`.

## Probe (second process)

```powershell
dotnet run --project d:\novolis\novolis-apps\src\SketchStudio -p:NovolisUseProjectReferences=true -- --agent-probe
```

Expects `AGENT_PROBE_OK` and a PNG under `%TEMP%\novolis-avalonia-agent\sketch-studio-agent-probe.png`.

## Stable AgentIds (`sketch.*`)

| Id | Control |
|----|---------|
| `sketch.window` / `sketch.viewport` / `sketch.status` | Window, canvas, status bar |
| `sketch.tool.*` | Pen, line, spline, rect, ellipse, speech, text, textbox, eraser, fill, select |
| `sketch.file.*` | New, open, recent, save, saveAs |
| `sketch.layers` / `sketch.layer.*` | Layer combo + add / visibility / lock |
| `sketch.toggle.*` | Snap, meetup, grid, fill |
| `sketch.export.*` | Copy/save PNG + SVG |
| `sketch.help.shortcuts` | F1 dialog |
