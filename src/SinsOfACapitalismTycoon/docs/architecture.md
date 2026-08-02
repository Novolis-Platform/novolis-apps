# Architecture

## Two engines

| Engine | CLI | Runtime | Purpose |
|--------|-----|---------|---------|
| **campaign** (default) | `--engine campaign --days Nd` | Astro catalog → EconomyWorld → Agents → EconomySimulation (1h ticks) | Product surface |
| **core** | `--engine core --scenario … --periods N` | Core `EconomyState` + host policies + period pipeline | BM regression / drama packs |

**Dual engines are intentional:** campaign is the product; core remains BM regression in the same exe (no project split).

Shell UI (`--mode headless|avalonia`) is orthogonal: headless prints Spectre (campaign) or
plain text (core); Avalonia is the **captain bridge** (live Calypso orders when `--player on`).

## Campaign pulse (components + events)

Day-end commerce is an ordered list of `ICampaignDayStep` units
(`Universe/Pulse/CampaignDayPipeline.cs`), not a hand-edited checklist in the clock.

| Piece | Role |
|-------|------|
| `CampaignPulse` | Hour loop (agents, Advance 1h, mesh sync) |
| `CampaignDayPipeline` | Ordered day-end steps (claims → escrow → insurance → …) |
| `SimEventCursor` | Consumes new `EconomyState.Events` without `TakeLast` windows |
| `CampaignNoticeBus` | Typed notice drain (escrow → mesh; soft-fail channel) |
| `IBerthAutopilotPolicy` | Autopilot berth decisions; tramp agent only executes |

Shells (Avalonia / CLI / bridge IPC) go through `CaptainBridgeService` → `CaptainActions` → `PlayerOrder` queue.



## Library vs app (fundamentals)

Reusable pieces live in packages; Sins owns campaign orchestration and Ops registry theater:

| Concern | Library | Sins still owns |
|---------|---------|-----------------|
| Hull premium / overhaul quote math | `Novolis.Economy.Logistics` (`HullRiskQuotes`, `FtlDriveLifePolicy`) | `CampaignRegistryBooks` + `ShipRegistry` standing, grace, liens, insurance pulses |
| Registry door (generic) | — (Sins-local) | `RegistryRecord` / `RegistryBook{T}` — ship, firm, license books |
| Tape-aware gate price | `Novolis.Economy.Markets` (`TapeAwareGatePricing`) | Floor/SKU seed constants, agent wiring |
| System roles from potentials | `Novolis.Astro.Assessment` (`SystemRole`, `RoleAssigner`, `SystemRoleInvariants`) | `AstroEconomyBridge`, HubOps dwell/berths, PortTier overlays |
| Confederation mesh (visibility) | — (Sins-local BM; Kernel vs Sins namespaces; future `Novolis.Mesh.Core`) | `Universe/Mesh/Kernel` + `Universe/Mesh/Sins` glue on campaign hour |

No shared `Novolis.Economy.Campaign` package yet — bridge + runner stay app-local.

Checkpoints are **deterministic replay** (seed → hours) plus integrity fields — not Economy world dumps (platform has no round-trip dump API).

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
