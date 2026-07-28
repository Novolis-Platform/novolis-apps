# CLI and reports

## Campaign (default)

```powershell
dotnet run --project novolis-apps/src/SinsOfACapitalismTycoon `
  -p:NovolisUseProjectReferences=true -- `
  --engine campaign --days 100d --seed 1001
```

| Arg | Default | Meaning |
|-----|---------|---------|
| `--engine campaign\|core` | `campaign` | Runtime |
| `--days Nd` | `10d` | Simulation days |
| `--seed U` | `1001` | Campaign seed |
| `--drama on\|off` | `on` | Fuel famine / production / fiscal / Calypso dock beats |
| `--story` / `--story on` | off | Live `[vox.*]` tickers + session overture on stderr |
| `--mode headless\|avalonia\|captain` | `headless` | Spectre / GUI desk / text REPL |
| `--player on\|off` | on in avalonia/captain / off headless | James / ST Calypso agency vs AI tramp |
| `--autopilot on\|off` | `off` | When player on: AI hauls if order queue empty |
| `--board local\|network` | `network` | Spot intel filter (`berth`/`local` = current hub only; accept still berth-gated) |
| `--commands "…"` | — | Captain script (`status;spot;travel X;accept 0;depart;…`) |
| `--playtest` | — | Built-in captain acceptance (agent-friendly) |
| `--quiet` / `-q` | off | Hide progress; still print report (headless) |

## Spectre narrative sections

1. Header — day, duration, world hash, drama flag
2. Radio overture — `[vox.*]` board check-in
3. Ops money + claims / escrow fees / actuarial load
4. Ops vs Core (never summed)
5. Logistics — WaitingBerth / Loading, wear, plan fails
5b. **Mesh** — hubs/edges, drones, directed/flood publishes, visibility credits
6. Registry (Rep / Life% / Lien) + port-tier sample
7. **Life moments** scorecard + **Milestones** (`MILESTONE:` + vox line)
8. Mega-hauler + top owner-master biographies
9. Market tape sample
10. Activity + agent last decisions
11. Curtain line

How to *play* the report: [gameplay.md](gameplay.md). Who is speaking: [characters.md](characters.md).
Mesh doctrine: [mesh-and-communications.md](mesh-and-communications.md).

## Avalonia captain desk

```powershell
dotnet run --project novolis-apps/src/SinsOfACapitalismTycoon `
  -p:NovolisUseProjectReferences=true -- `
  --engine campaign --days 30d --seed 1001 --mode avalonia --player on
```

Live cockpit for **ST Calypso**: voyage strip, star map (travel target + berth highlight),
Spot | Charters | Manifest tabs, Step 1d / Continue / To horizon. See network intel;
accept spot only at load berth; travel empty with **Travel here**. Time keeps moving until
a decision is needed.

## Text captain / playtest (agent-friendly)

```powershell
dotnet run --project novolis-apps/src/SinsOfACapitalismTycoon `
  -p:NovolisUseProjectReferences=true -- `
  --engine campaign --days 60d --seed 1001 --playtest

dotnet run --project novolis-apps/src/SinsOfACapitalismTycoon `
  -p:NovolisUseProjectReferences=true -- `
  --engine campaign --days 30d --seed 1001 --mode captain `
  --commands "status;spot;travel-to-best;continue;accept-at-berth;depart;resume"
```

Captain verbs: `travel`, `spot`, `charters`, `accept N`, `manifest`, `depart`, `refuse`.
Requires `Novolis.Avalonia.Briefing` (+ StarMap, Studio, Agent) on GitHub Packages `2026.1.*`, or
ProjectRef for local sibling checkouts. Avalonia agent smoke: [AGENT-SMOKE.md](../AGENT-SMOKE.md).
Mid-tick map animation stays on the roadmap.

## Core smoke

See [scenarios-core-smoke.md](scenarios-core-smoke.md).
