# CLI and reports

## Campaign (default)

```powershell
dotnet run --project novolis-apps/src/SinsOfACapitalismTycoon `
  -p:NovolisUseProjectReferences=true -- `
  --engine campaign --days 10d --seed 1001
```

| Arg | Default | Meaning |
|-----|---------|---------|
| `--engine campaign\|core` | `campaign` | Runtime (`universe` / `polity` still accepted) |
| `--days Nd` | `10d` | Simulation days (`10d`, `100d`, `1000d`) |
| `--seed U` | `1001` | Campaign seed |
| `--mode headless\|avalonia` | `headless` | Output shell |
| `--quiet` / `-q` | off | Hide progress bars; still print final report |

Artifact (when not quiet): `artifacts/sins-report-{days}d.txt` under the apps artifacts path when writable.

## Spectre report sections (campaign)

1. Header — seed, days, wall time, world hash
2. Money — Ops liquid Δ; never “Ops+Core”
3. Core projected accounts — sectoral NW, unpriced holdings
4. Ops ledgers — firm cash / revenue / notes
5. Logistics — shipments by phase, cargo in flight, toll exposure
6. Activity — produced / retail / delivered
7. Agents — last decisions sample
8. Geography — roles / hubs / corridors counts
9. Milestones — credit circulation samples when present

## Core smoke

See [scenarios-core-smoke.md](scenarios-core-smoke.md).
