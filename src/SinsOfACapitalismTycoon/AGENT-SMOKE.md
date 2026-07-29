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
| `calypso.voyage` / `calypso.hull` / `calypso.decision` / `calypso.survival` | Voyage strip (+ last-tramp line) |
| `calypso.map` | Star map |
| `calypso.travelSystem` | Typed travel destination (system id) |
| `calypso.travel` | Travel here |
| `calypso.step` / `calypso.continue` / `calypso.resume` / `calypso.pause` / `calypso.save` | Transport + checkpoint |
| `calypso.boards` | Spot \| Charters \| Manifest tabs |
| `calypso.spot` / `calypso.charters` / `calypso.manifest` | Lists |
| `calypso.boardScope` / `calypso.profile` | Combos |
| `calypso.acceptSpot` / `calypso.depart` | Berth acts |
| `calypso.acceptStandby` / `calypso.refuseStandby` | Charters |
| `calypso.wait` / `calypso.premium` / `calypso.overhaul` | Hull acts |

## MCP checks

Enable `avalonia-agent` in Cursor (`.cursor/mcp.json`), then:

1. `ui_hosts` → `ui_connect` `endpoint=novolis-avalonia-agent-sins` (Sins dedicated pipe)
2. `ui_hello` — title contains `Captain Desk` / `Calypso`
3. `ui_get` ids=`calypso.voyage,calypso.survival,calypso.continue,calypso.decision` — compact desk
4. `ui_items` `calypso.spot` — spot lines with indexes (no select needed)
5. `ui_select` `calypso.boards` `itemText=Spot` — Spot tab
6. `ui_type` `calypso.travelSystem` `ez-aquarii` `clear=true` then `ui_click` `calypso.travel`
7. `ui_click` `calypso.continue` then `ui_poll` `calypso.voyage` `textContains=BERTH` (prefer poll over wait while sim runs)
8. `ui_select` `calypso.spot` `itemText=AT BERTH` then `ui_click` `calypso.acceptSpot`
9. `ui_screenshot` — PNG under `%TEMP%/novolis-avalonia-agent/`

## Last-tramp win (autopilot)

```powershell
$env:NOVOLIS_AVALONIA_AGENT = "1"
dotnet run --project d:\novolis\novolis-apps\src\SinsOfACapitalismTycoon `
  -p:NovolisUseProjectReferences=true -- `
  --mode avalonia --days 120d --seed 1001 --last-tramp --autopilot on
```

Headless acceptance: `--playtest-last-tramp` → `PLAYTEST LAST-TRAMP PASS`.
Agent: `ui_poll` `calypso.survival` `textContains=WIN`.

Protocol `1.1`: `ui.get` / `ui.items`; lists in `ui.tree` emit `[i]` item rows.
