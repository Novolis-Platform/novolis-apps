# Sins of a Capitalism Tycoon — documentation

Institutional notes for operators and contributors. Fiction tone for cargo and registry
borrows from the Confederation [Standards and Infrastructure](https://frankhaugen.github.io/galactic-confederation-review/series/standards-and-infrastructure/)
series: standards are interfaces that make trade possible, not ceremonies.

## Start here

1. [Vision](vision.md) — what “mind-boggling” means in this app
2. [Architecture](architecture.md) — campaign engine vs core-smoke; Ops vs Core books
3. [Universe](universe.md) — 100 nearest systems, hop graph, roles
4. [Standards and cargo](standards-and-cargo.md) — C-series fiction mapped to code
5. [Agents and firms](agents-and-firms.md) — who decides each hour
6. [CLI and reports](cli-and-reports.md) — headless duration, Spectre sections
7. [Core smoke scenarios](scenarios-core-smoke.md) — BM regression packs
8. [Roadmap](roadmap.md) — explicitly later

## Product path (default)

```powershell
dotnet run --project novolis-apps/src/SinsOfACapitalismTycoon `
  -p:NovolisUseProjectReferences=true -- --engine campaign --days 10d --seed 1001
```

## Core smoke (regression only)

```powershell
dotnet run --project novolis-apps/src/SinsOfACapitalismTycoon `
  -p:NovolisUseProjectReferences=true -- --engine core --scenario baseline --periods 50 --quiet
```
