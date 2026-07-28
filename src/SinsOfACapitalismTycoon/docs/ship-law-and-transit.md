# Ship law, registry, and FTL tradeoffs

Fiction sources (commerce-related):

- [The Margin Was The Freedom](https://frankhaugen.github.io/galactic-confederation-review/articles/the-margin-was-the-freedom/)
- [Ship Law and Registry dossier](https://frankhaugen.github.io/galactic-confederation-review/dossiers/ship-law-and-registry/)
- [FTL Transit Operational Tradeoffs](https://frankhaugen.github.io/galactic-confederation-review/articles/ftl-transit-operational-tradeoffs/)

## Thesis

Registry is a **door**, not romance. Insurance prices freedom. FTL does not abolish logistics —
it makes speed an expensive operating choice. Small ships work the gap between efficient and necessary.

Drive stacks have a **rated life**. Speed (profile), distance (hours × difficulty), and mass (cargo load)
burn that life. You overhaul in the elective window, or you wait and **guarantee burnout**.

## Mapped to campaign code

| Fiction | Code |
|---------|------|
| Registry record / standing | `ShipRegistry` — insured, suspended, burned-out, overhaul-due, lien |
| Owner-master vs fleet | Tramps + ventures register as owner-master; mega as fleet |
| Insurance premium | `InsurancePulse` daily; scales with **life fraction**, capped (not a terminal tax) |
| Claims (loss ≠ erase) | `ClaimsPulse` on stall-abandon / Priority wear; deductible |
| Mileage / mass / speed | Library `TransitProfiles.WearForUnderwayHour(profile, load, difficulty)` |
| Rated life → burnout | `LifeUsed` vs `RatedLife`; `DriveMaintenancePulse` |
| Elective vs forced overhaul | Quote elective @ 72% life; burnout overhaul ~2.15× |
| Maintenance cash | Overhaul posts to yard/underwriter — separate from premium |
| CCA escrow + fees | `EscrowBook` — 5% issuer / ≥10% contractor skim |
| Reputation → work | `ReputationLedger` + `EffectiveMinMargin` / standby pick |
| Ugly standby pool | `OpportunitiesPool` — refusal ≠ premium (`standby-pass`) |
| Jump-band refuse | `JumpBandGate` + `CarrierFirmAgentPolicy.RefuseHaul` |
| Port tiers | `PortTier` on roles → dwell/toll/berth fee |
| Debt follows hull | `LienPrincipal` + `LienPulse` |
| Big carriers vs edges | `MV Bulk River` Slow bulk + tramp Priority Final |
| Slow / Standard / Priority | `TransitProfile` scales hours, fuel, wear |
| Fuel geography | Thin Transit/Waypoint bunkers; drama fuel famine days |
| Berth congestion | Tighter Capital berths; `AvoidHub` when WaitingBerth ≥ capacity |
| Drama shocks | `CampaignDramaHost` (`--drama on\|off`) |
| Narrative | `MilestoneLog` / Spectre `MILESTONE:` + tramp biographies |

## Operator check

After `100d`, Spectre should show greppable `MILESTONE:` lines for grounding, fuel-famine,
claim, upgrade, mega deliveries — and Ops vs Core never summed. Longer runs should show
`overhaul` / `burnout` rather than a permanently uninsured fleet.
