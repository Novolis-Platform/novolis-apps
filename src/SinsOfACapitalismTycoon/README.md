# Sins of a Capitalism Tycoon

Interstellar campaign dogfood: **100 near-Sol systems**, hop freight, firms and households
that must pay and haul. Primary path is Simulation + Astro + Agents. Core period scenarios
remain available as regression smoke only.

## Documentation

Full operator notes: **[docs/README.md](docs/README.md)**  
(architecture, universe, standards/cargo fiction, agents, CLI, roadmap).

## Run (campaign — default)

```powershell
cd novolis-apps
dotnet run --project src/SinsOfACapitalismTycoon -p:NovolisUseProjectReferences=true -- --engine campaign --days 10d --seed 1001
dotnet run --project src/SinsOfACapitalismTycoon -p:NovolisUseProjectReferences=true -- --engine campaign --days 100d --seed 1001 --quiet
```

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
