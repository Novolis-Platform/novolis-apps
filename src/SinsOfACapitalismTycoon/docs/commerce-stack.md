# Commerce stack (CCA / Meridian / HILS / CR)

Adapted from Calypso Cycle `references/` — cargo, registry, CCA, insurance entities,
daily-life-and-trade, jump cheatsheet. Staged for Sins; partial code today.

## The boring OS that makes markets possible

The **Galactic Confederation** is not a planetary emperor. It is a **trade and registry
operating system**: credits, titles, insurance, escrow, port access. Fair because it
measures everyone the same. Brutal because it measures only what it can see.

Independent operators experience that OS through **CCA-shaped** routines:

- Posted work with insurance minima and escrow releases
- Registry checks on hulls, liens, liability chains
- Inspections that differ by **port tier** and polity host

Ixa-style commercial legwork is not “hustle on empty space.” It is navigation of a
bureaucracy that is **boring on purpose**.

## CCA (Confederate Commerce Authority) — job board fiction

| Piece | Canon shape | Sins mapping (now / later) |
|-------|-------------|----------------------------|
| Posted work | Cargo / charter / courier behind registration | Hub orders + tramp haul (now); captain Spot vs Charters intel (now) |
| Escrow | Payment held until delivery confirmation | `EscrowBook` Ops hold + release / clawback; principal = staged **dest bid × qty** when captain accepts a haul (else catalog unit) |
| Issuer fee | **~5%** of contract value (client) | Station books issuer fee on escrow open (now) |
| Contractor insurance | **≥10%** + risk modifiers (contractor) | Escrow contractor skim ≥10% → UW + `InsurancePulse` (now) |
| Blacklist | Economic death; unregistered counterparties void cover | Registry suspend / uninsured / lien hold (now) |
| Public liability check | Anyone can inspect hull debt status | Spectre registry + `Lien` column (now) |
| Reputation | Known-responsive → future work | `ReputationLedger` lowers MinMargin; standby preference (now) |
| Opportunities | Ugly standby; refusal ≠ premium | `OpportunitiesPool` recurring offers (now) |
| Jump bands | Dense Priority refuse | `JumpBandGate` + `RefuseHaul` (now) |
| Port tiers | Capital / refinery / edge friction | `PortTier` dwell/toll/dock fee (now) |

**Aphorism:** plenty of opportunity — all of it locked behind registration.
Captain bridge adds: **plenty of opportunity; acceptance is a dock act** — mesh intel,
empty `PlanReposition` travel, dock-gated spot accept, separate Spot vs Charter panels.

### Quote engine (tramp margin — flavor numbers)

Civilian ST tramp bands (Cycle cheatsheet; tune in sim later):

| Job shape | Rough CR band |
|-----------|----------------|
| ~6 ly light ferry | 6–9k |
| ~10 ly pax + light | 12–18k |
| ~10 ly dense | 22–32k |
| Dense + push | 35–50k+ |
| ~12 ly empty reposition | 16–24k |
| 12 ly dense sprint | Refuse unless someone else owns the drive |

First small charter tone: relocation / low risk / local origin — **~15,000 CR**.

## Credits (lived experience)

- **1 CR** ≈ effort unit; **10 CR** ≈ 1 day unskilled hand labor
- Skilled **50–100+ CR/day**; thin liquidity two jumps out; fees eat small balances
- **Debt follows the hull** — inheritance can mean obligations, not just keys
- Ledger is **morally blind** (coerced vs free labor posts the same)

Sins Ops ledgers are the simulation truth; CR flavor annotates player-facing copy.
Never invent “total cash” = Ops + Core.

## Insurance stack (Blue Meridian family)

| Entity | Role | Sins voice |
|--------|------|------------|
| **Blue Meridian Underwriters** | Boring insurer you want; drama-hating | Station underwriter / `vox.broker` |
| **Blue Meridian Maritime** | Hull, LS, crew injury, contract interruption | Premium + claim lines |
| **Meridian Continuity / Keystone** | Keystone-operator succession; asset-freeze prevention | Venture / owner-master death later |
| **Opportunities Registry** | Opt-in charter Rolodex; **refusal ≠ premium hit** | Drama standby / ugly money jobs |
| **Cinderline** | Continuity/protection contractor (street work insurers outsource) | Soft pickup / threshold (flavor) |

Praise that means something: **“Odd vessel. Stable risk.”**

Governing Meridian principle (Eska arc):

> Finance decides what can be spent. Operations decides when it must be spent.

Standby ugly money tells: *Hundred thousand on completion… risk premium waived for the
standby window* → *That’s ugly money… which means the job is ugly or the person is expensive.*

Civilian completion crews chase **completion**, not momentum. Selection language stays
stilted: *selection support only; no intervention specified, requested, or priced.*

## HILS cargo grammar

Founding interface (Cycle `references/cargo/`):

| Unit | Meaning |
|------|---------|
| **HILS-P1 / PM** | EURO pallet atomic unit (1200×800); ferro edge; optical+RF ID |
| **C10 / C20 / C40** | Container length family; W×H fixed; PM capacity bands |
| Accounting | Integers — PM / C-eq / kg·PM⁻¹ — **never volume alone** |

**Aphorism:** *If it fits the pallet, it fits the galaxy.*

Sins v1 uses hull cargo capacity + corridor `MaxCargo` as envelopes
([standards-and-cargo.md](standards-and-cargo.md)). Full HILS SKUs stay roadmap — but
flavor and future enforcement speak HILS.

## Jump doctrine (ops, not magic)

Writer shorthand: **ten light-years is routine, twelve hurts, beyond that you need a reason.**

| Band | Civilian feel |
|------|----------------|
| 6–8 ly | Comfortable |
| ~10 ly | Routine |
| ~12 ly | Painful |
| Beyond | Tech max / justification |

Spend: mass hurts; distance hurts badly; **speed burns drive life**. Chaining short hops
beats one long sprint. Dense feedstock ≈ harder than light ferry same hop.

Sins corridor baseline (~1.3 d/ly) + `TransitProfile` is the sim expression of that brief.

## Crime spectrum (soft regulator, not a second game)

Tariff dodge → cargo fraud → small piracy → Tortuga fencing → Cartel industrialization.
Enforcement without a universal navy: escrow clawback, lien, registry hold, blacklist,
licensed repo/salvage, Nosies on irregular seizures, Fleet only where merchant corridors
justify presence.

**Smuggling ladder root:** *I do not want to pay what the legal corridor costs.*

## Species as customers (modifiers later)

| Species | Trade psychology |
|---------|------------------|
| Vaelor | Worst outcome first; walk from unquantified risk |
| Kesh’lin | Relationship > optimal deal; social debt |
| Orunai | Paper prettier than paint; no small talk |
| Enerethi | Triad consensus; hate unilateral CEOs |
| Humans / Earthers | Procedural aggression; Fleet shadow on corridors |

Registry ≠ membership. Tags drive atmosphere / AutoDoc / labor fiction later.

## Related

- [Calypso canon](calypso-canon.md)
- [Places and stations](places-and-stations.md)
- [Ship law and transit](ship-law-and-transit.md)
- [Standards and cargo](standards-and-cargo.md)
