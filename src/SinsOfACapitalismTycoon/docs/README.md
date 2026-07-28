# Sins of a Capitalism Tycoon — documentation

Institutional notes, **gameplay**, **cast**, **voice**, and a **Calypso Cycle** lore bridge.
Fiction tone borrows from Confederation
[Standards and Infrastructure](https://frankhaugen.github.io/galactic-confederation-review/series/standards-and-infrastructure/)
and the commercial weather of **The Calypso Cycle** (`references/` — CCA, Meridian, HILS, stations).

## Start here (players)

1. [Gameplay](gameplay.md) — loops, life moments, how to read a run
2. [Characters](characters.md) — houses, hulls, Calypso faces, `vox.*` ids
3. [Flavor and audio](flavor-and-audio.md) — transmissions, VO, stage directions

## Universe bible (Calypso → Sins)

4. [Calypso canon](calypso-canon.md) — provenance and adaptation rules
5. [Commerce stack](commerce-stack.md) — CCA, Meridian, escrow, HILS, CR, jump brief
6. [Places and stations](places-and-stations.md) — Duckville→Tortuga tiers on roles
6b. [Mesh and communications](mesh-and-communications.md) — publish/propagate BM sim

## Operators / contributors

7. [Vision](vision.md) — what “mind-boggling” means
8. [Architecture](architecture.md) — campaign vs core-smoke; Ops vs Core
9. [Universe](universe.md) — 100 nearest systems, hop graph, roles
10. [Standards and cargo](standards-and-cargo.md) — C-series / HILS mapped to code
11. [Ship law and transit](ship-law-and-transit.md) — registry, insurance, FTL profiles
12. [Agents and firms](agents-and-firms.md) — who decides each hour
13. [CLI and reports](cli-and-reports.md) — flags, Spectre sections
14. [Core smoke scenarios](scenarios-core-smoke.md) — BM regression packs
15. [Roadmap](roadmap.md) — explicitly later

## Product path (default)

```powershell
dotnet run --project novolis-apps/src/SinsOfACapitalismTycoon `
  -p:NovolisUseProjectReferences=true -- --engine campaign --days 10d --seed 1001
```

Long quiet run — hunt `MILESTONE:`:

```powershell
dotnet run --project novolis-apps/src/SinsOfACapitalismTycoon `
  -p:NovolisUseProjectReferences=true -- --engine campaign --days 100d --seed 1001 --quiet
```

## Core smoke (regression only)

```powershell
dotnet run --project novolis-apps/src/SinsOfACapitalismTycoon `
  -p:NovolisUseProjectReferences=true -- --engine core --scenario baseline --periods 50 --quiet
```
