# Sins of a Capitalism Tycoon

Interstellar campaign dogfood that wants to feel like a **port cooperative memoir**:
**100 near-Sol systems**, hop freight, owner-master tramps, a mega-hauler, registry doors,
and insurance that prices freedom without making it comfortable.

Primary path is Simulation + Astro + Agents. Core period scenarios remain regression smoke only.

## Documentation

**Start with play and voice:** [docs/gameplay.md](docs/gameplay.md) · [docs/characters.md](docs/characters.md) · [docs/flavor-and-audio.md](docs/flavor-and-audio.md)

**Calypso lore bridge:** [docs/calypso-canon.md](docs/calypso-canon.md) · [docs/commerce-stack.md](docs/commerce-stack.md) · [docs/places-and-stations.md](docs/places-and-stations.md)

Full index: **[docs/README.md](docs/README.md)**

## Run (campaign — default)

```powershell
cd novolis-apps
dotnet run --project src/SinsOfACapitalismTycoon -p:NovolisUseProjectReferences=true -- --engine campaign --days 10d --seed 1001
dotnet run --project src/SinsOfACapitalismTycoon -p:NovolisUseProjectReferences=true -- --engine campaign --days 100d --seed 1001 --quiet
```

After a quiet run, hunt `MILESTONE:` — grounding, famine, claims, upgrades, defaults.

## Run (core smoke)

```powershell
dotnet run --project src/SinsOfACapitalismTycoon -p:NovolisUseProjectReferences=true -- --engine core --scenario baseline --periods 50 --seed 42 --quiet
```

## Local package sources

Use ProjectReference mode for sibling Economy/Astro packages:

```powershell
dotnet build src/SinsOfACapitalismTycoon -p:NovolisUseProjectReferences=true
```

No local NuGet folder feeds. Publish to GitHub Packages when CI consumers need new APIs.
