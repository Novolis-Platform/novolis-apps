# Draft Studio agent smoke

Wire-up uses the shared Avalonia agent protocol (`Novolis.Avalonia.Agent`) and Cursor MCP `avalonia-agent`.

## Build / tests

```powershell
dotnet build d:\novolis\novolis-apps\src\DraftStudio -p:NovolisUseProjectReferences=true
dotnet test d:\novolis\novolis-apps\tests\DraftStudio.Unit -p:NovolisUseProjectReferences=true
```

## Run Draft Studio with agent host

```powershell
$env:NOVOLIS_AVALONIA_AGENT = "1"
dotnet run --project d:\novolis\novolis-apps\src\DraftStudio -p:NovolisUseProjectReferences=true
```

## MCP checks (commands → save → inspect → dump)

1. `UiHello` — title contains `Draft Studio`
2. `UiType` into `draft.commandBar`: `Level(3)` then `Rect(0,0,8,6)` then `Box(2,1,3)`
3. `UiType` `Save` — or `UiClick` `draft.tool.save`
4. Read `%LocalAppData%\Novolis\Draft Studio\dumps\last-document.path` (or workspace document) and inspect `.cadjson`
5. `UiType` `Dump` — or `UiClick` `draft.dump`
6. Read `dumps/last-artifact.json` for `documentPath`, `draftPngPath`, `modelPngPath`, `windowPngPath`
7. `UiScreenshot` — Avalonia UI capture; model PNG uses Raylib presented framebuffer via `RaylibHostControl.TrySaveLastPresentedFramePng`

## Agent ids (extras)

| Id | Control |
|----|---------|
| `draft.dump` | Dump artifacts button |
| `draft.elevation` | Level NumericUpDown |
| `draft.continuous` | Continuous line checkbox |
| `draft.shapes` | Shape strip above command bar |
