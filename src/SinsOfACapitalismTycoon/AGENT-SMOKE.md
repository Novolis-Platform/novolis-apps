# Sins captain desk — Avalonia agent smoke

Wire-up uses `Novolis.Avalonia.Agent` (+ Protocol) and Cursor MCP `avalonia-agent`.
Text captain (`--mode captain` / `--playtest`) remains the fast headless path; this smoke drives the GUI desk.

## Build

```powershell
dotnet build d:\novolis\novolis-avalonia\src\Novolis.Avalonia.Agent -p:NovolisUseProjectReferences=true
dotnet build d:\novolis\novolis-apps\src\SinsOfACapitalismTycoon -p:NovolisUseProjectReferences=true
dotnet build d:\novolis\novolis-dogfooding\apps\AvaloniaAgentMcp -p:NovolisUseProjectReferences=true
```

## Run captain desk with agent host

```powershell
$env:NOVOLIS_AVALONIA_AGENT = "1"
dotnet run --project d:\novolis\novolis-apps\src\SinsOfACapitalismTycoon `
  -p:NovolisUseProjectReferences=true -- `
  --engine campaign --days 30d --seed 1001 --mode avalonia --player on
```

## Stable AgentIds (`calypso.*`)

| Id | Control |
|----|---------|
| `calypso.voyage` / `calypso.hull` / `calypso.decision` | Voyage strip |
| `calypso.map` | Star map |
| `calypso.travelSystem` | Typed travel destination (system id) |
| `calypso.travel` | Travel here |
| `calypso.step` / `calypso.continue` / `calypso.resume` / `calypso.pause` | Transport |
| `calypso.boards` | Spot \| Charters \| Manifest tabs |
| `calypso.spot` / `calypso.charters` / `calypso.manifest` | Lists |
| `calypso.boardScope` / `calypso.profile` | Combos |
| `calypso.acceptSpot` / `calypso.depart` | Berth acts |
| `calypso.acceptStandby` / `calypso.refuseStandby` | Charters |
| `calypso.wait` / `calypso.premium` / `calypso.overhaul` | Hull acts |

## MCP checks

Enable `avalonia-agent` in Cursor (`.cursor/mcp.json`), then:

1. `UiHello` — title contains `Captain Desk` / `Calypso`
2. `UiTree` — expect `calypso.travel`, `calypso.spot`, `calypso.boards`, `calypso.voyage`
3. `UiSelect` `controlId=calypso.boards`, `itemText=Spot` — Spot tab
4. `UiType` `controlId=calypso.travelSystem`, `text=ez-aquarii`, `clear=true` then `UiClick` `calypso.travel`
5. `UiSelect` `controlId=calypso.spot`, `index=0` then `UiClick` `calypso.acceptSpot` (only when AT BERTH)
6. `UiScreenshot` — PNG under `%TEMP%/novolis-avalonia-agent/`

Library note: `ui.select` selects ListBox / ComboBox / TabControl by index or text substring.
