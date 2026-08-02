# Capitalist Simulator — gameplay

Capitalism 2 homage: build firms on city maps, wire functional units, compete on **price / quality / brand**, expand across cities, and use bank + stock tools.

## Run

```powershell
dotnet run --project d:\novolis\novolis-apps\src\CapitalistSimulator -p:NovolisUseProjectReferences=true -- --mode headless --days 36
dotnet run --project d:\novolis\novolis-apps\src\CapitalistSimulator -p:NovolisUseProjectReferences=true -- --mode avalonia
```

## Loop

1. Pick a city tile → **Build** (retail / factory / farm / extract / R&D / HQ).
2. Open firm interior → place units, **Auto-Link**, configure sales slots (4 max in retail) and purchasing.
3. Factories need recipes + input stock (seaport or extract/farm).
4. Advertise (brand strategy: Corporate / Range / Unique).
5. Borrow, trade shares, set dividends; HQ toggles automate light chores.
6. Advance months (`+30d` or Speed > 0). Win scenarios: `RetailProfit`, `WineDominance`, or free `Sandbox`.

## Saves

`%LocalAppData%\Novolis\CapitalistSimulator\saves\`

## Design sources

Semantics lifted from Capitalism 2 manuals/tutorials/`1STD.SET` product relationships. No proprietary Cap2 binaries or art are shipped.
