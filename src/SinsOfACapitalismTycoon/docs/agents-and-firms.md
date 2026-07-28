# Agents and firms

Campaign hour tick order: **agents decide, then `EconomySimulation.AdvanceAsync(1h)`**.

## Library agents (`Novolis.Economy.Agents`)

| Role | Agent | Notes |
|------|-------|-------|
| Mining | `ExtractiveFirmAgent` | Raw at mining hubs |
| Industry | `ManufacturingFirmAgent` | Raw → Capital / Final |
| Station retail | `RetailFirmAgent` | Final to cohorts |
| Treasury | `TreasuryFirmAgent` | Station-side fiscal |
| Carriers / tramps | `CarrierFirmAgent` | Homes cycle Sol / mines / plants |
| Households | `HouseholdFirmAgent` | Comfort invest when above floor |

## Sins-local agents

| Agent | Behavior |
|-------|----------|
| Sol export hub | Overflow buy below industry price; export dump above soft store limit |
| Household tramp venture | Comfortable HH → hull loan tramp (may be pulse-gated) |

## Consumption sink

Households spend on **Final** (Goods). Wages refill cohort budgets; retail destroys Final
stock. That loop is the intended equilibrium / growth driver. Capital parts stay B2B.

## Chaos knobs (v1)

- Soft/hard store limits at Capital
- Hull cargo ~36, corridor max ~48 (campaign-tuned)
- Capital dwell / berth pressure for volume and time binding

Agents are heuristic + `DeterministicRandom` — not ML.
