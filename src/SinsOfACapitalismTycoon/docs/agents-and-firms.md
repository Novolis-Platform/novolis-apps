# Agents and firms

Campaign hour tick order: **agents decide, then `EconomySimulation.AdvanceAsync(1h)`**.
Each campaign day also runs `ClaimsPulse` → `DriveMaintenancePulse` → `EscrowBook` →
berth fees / `LienPulse` → `InsurancePulse`, plus morning reinstate and Opportunities /
reputation ticks from drama.

For cast, sins, and voice ids, see **[characters.md](characters.md)**. For how to watch
a run, see **[gameplay.md](gameplay.md)**.

## Library agents (`Novolis.Economy.Agents`)

| Role | Agent | Notes |
|------|-------|-------|
| Mining | `ExtractiveFirmAgent` | Raw at mining hubs — “urgent” forever |
| Industry | `ManufacturingFirmAgent` | Raw → Capital / Final — optimistic schedules |
| Station retail | `RetailFirmAgent` | Final to cohorts — bad chairs, real Confederation |
| Treasury | `TreasuryFirmAgent` | Station-side fiscal |
| Carriers / tramps | `CarrierFirmAgent` | Owner-masters; cargo-chosen `TransitProfile` |
| Mega-hauler | `CarrierFirmAgent` | `MV Bulk River` — SlowEconomic only |
| Capacity | `CapacityInvestAgent` | UpgradeFacility when starved / flush |
| Loan repay | `LoanRepayAgent` | Due-soon repay before default |
| Drama | `CampaignDramaHost` | Fuel famine, ore shock, fiscal bleed |

## Sins-local agents

| Agent | Behavior |
|-------|----------|
| Sol export hub | Overflow buy below industry price; export dump above soft store limit |
| Household tramp venture | Comfortable HH → hull loan tramp (cast expansion when enabled) |

## Consumption sink

Households spend on **Final** (Goods). Wages refill cohort budgets; retail destroys Final
stock. That loop is the intended equilibrium / growth driver. Capital parts stay B2B.

## Chaos knobs (v1)

- Soft/hard store limits at Capital
- Hull cargo ~36, corridor max ~48 (campaign-tuned)
- Capital dwell / berth pressure for volume and time binding

Agents are heuristic + `DeterministicRandom` — not ML. Characterization is documentary,
not a second AI.
