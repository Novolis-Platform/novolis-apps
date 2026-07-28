# Architecture

## Two engines

| Engine | CLI | Runtime | Purpose |
|--------|-----|---------|---------|
| **campaign** (default) | `--engine campaign --days Nd` | Astro catalog → EconomyWorld → Agents → EconomySimulation (1h ticks) | Product surface |
| **core** | `--engine core --scenario … --periods N` | Core `EconomyState` + host policies + period pipeline | BM regression / drama packs |

Shell UI (`--mode headless|avalonia`) is orthogonal: headless prints Spectre (campaign) or
plain text (core); Avalonia is the **captain desk** (live Calypso orders when `--player on`).

## Layering

```text
Johnston 100-star catalog (embedded JSON)
  → RouteGraph (≤12 ly) + SystemProfiles + Roles
  → AstroEconomyBridge → hubs / corridors on EconomyWorldBuilder
  → CampaignWorld seed (firms, SKUs, facilities, cohorts, tramp fleet)
  → EconomySimulation + AgentScheduler (agents then Advance 1h)
  → SpectreHeadlessReport (Ops vs Core never mixed)
```

Astro is **seed and geography**. Runtime authority for ops commerce is Simulation;
Core advances at period boundaries and remains the BM stock authority when bridged.

## Library vs app (fundamentals)

Reusable pieces live in packages; Sins owns campaign orchestration and Ops registry theater:

| Concern | Library | Sins still owns |
|---------|---------|-----------------|
| Hull premium / overhaul quote math | `Novolis.Economy.Logistics` (`HullRiskQuotes`, `FtlDriveLifePolicy`) | `CampaignRegistryDesk` + `ShipRegistry` standing, grace, liens, insurance pulses |
| Registry door (generic) | — (Sins-local) | `RegistryRecord` / `RegistryBook{T}` — ship, firm, license books |
| Tape-aware gate price | `Novolis.Economy.Markets` (`TapeAwareGatePricing`) | Floor/SKU seed constants, agent wiring |
| System roles from potentials | `Novolis.Astro.Assessment` (`SystemRole`, `RoleAssigner`, `SystemRoleInvariants`) | `AstroEconomyBridge`, HubOps dwell/berths, PortTier overlays |
| Confederation mesh (visibility) | — (Sins-local BM; future `Novolis.Mesh.Core`) | `Universe/Mesh` + `MeshBridge` / `MeshPulse` on campaign hour |

No shared `Novolis.Economy.Campaign` package yet — bridge + runner stay app-local.

## Ops vs Core money

- **Ops:** `FirmLedger` cash, invoices, notes — Simulation Accounting.
- **Core:** vault cash, deposits, loans, obligations — `Novolis.Economy.Core`.
- Reports must label `Ops cash` and `Core cash/broad money` separately.
  See `Novolis.Economy.Simulation.Extensions.WorldReportSnapshot` and
  `Novolis.Economy.Core.Extensions.ProjectedAccounts`.

## ProjectReference mode

Committed csproj uses PackageReference only. Local multi-repo iteration:

```powershell
dotnet build … -p:NovolisUseProjectReferences=true
```

Substitution is intersect-only (map ∩ PackageReference). No local NuGet folder feeds.
