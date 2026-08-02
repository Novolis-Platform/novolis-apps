# Capitalist Simulator

Avalonia + headless **Capitalism 2 homage**: firm/unit supply chains, P/Q/B retail competition, bank & stock bridge.

**Local app** — not in the Windows installer / release catalog yet.

## Run

```powershell
dotnet run --project d:\novolis\novolis-apps\src\CapitalistSimulator -p:NovolisUseProjectReferences=true -- --mode avalonia
dotnet run --project d:\novolis\novolis-apps\src\CapitalistSimulator -p:NovolisUseProjectReferences=true -- --mode headless --days 36
```

## Packages

| Package | Role |
|---------|------|
| Avalonia / Desktop / Fluent / DataGrid | Bridge UI |
| Novolis.Avalonia.Studio | Status / flash / busy |
| Novolis.Avalonia.Briefing | News feed |
| Novolis.Avalonia.Controls | Shared controls |
| Novolis.Storage.Json | Save DTOs (JSON files) |
| Spectre.Console | Headless report |

## Docs

- [docs/gameplay.md](docs/gameplay.md)
