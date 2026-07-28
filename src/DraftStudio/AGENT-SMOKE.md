# Draft Studio agent smoke

Wire-up uses the shared Avalonia agent protocol (`Novolis.Avalonia.Agent`) and Cursor MCP `avalonia-agent`.

## Build

```powershell
dotnet build d:\novolis\novolis-apps\src\DraftStudio -p:NovolisUseProjectReferences=true
dotnet build d:\novolis\novolis-dogfooding\apps\AvaloniaAgentMcp -p:NovolisUseProjectReferences=true
```

## Run Draft Studio with agent host

```powershell
$env:NOVOLIS_AVALONIA_AGENT = "1"
dotnet run --project d:\novolis\novolis-apps\src\DraftStudio -p:NovolisUseProjectReferences=true
```

## MCP checks

Enable the `avalonia-agent` server in Cursor (`.cursor/mcp.json`), then:

1. `UiHello` — expect title containing `Draft Studio`
2. `UiTree` — expect `draft.tool.line`, `draft.undo`, `draft.entities`, `draft.commandBar`
3. `UiClick` with `controlId=draft.tool.line` — Line tool active
4. `UiType` with `controlId=draft.commandBar`, `text=Box(1,1,1)`, `keys=["Enter"]` — entity appears
5. `UiScreenshot` — PNG under `%TEMP%/novolis-avalonia-agent/`
