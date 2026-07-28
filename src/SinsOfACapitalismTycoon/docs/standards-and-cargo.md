# Standards and cargo

Fiction inspiration: [C-Series Containers and the Founding Standard](https://frankhaugen.github.io/galactic-confederation-review/articles/c-series-containers-founding-standard/)
and the wider [Standards and Infrastructure](https://frankhaugen.github.io/galactic-confederation-review/series/standards-and-infrastructure/) series.

## Thesis (fiction)

Civilization announces itself in law. It continues because the cargo can be unloaded.
A founding standard is a **narrow durable interface**: few sizes, common locks, plates that
declare ratings, hazard marks that survive mixed ports. It does not make trade fair. It
makes participation less dependent on knowing every port’s private habits.

## Mapped to Sins v1 code

| Fiction | Code (now) | Code (later — see roadmap) |
|---------|------------|----------------------------|
| C10 / C20 / C40 quantum | Hull cargo capacity & corridor `MaxCargo` as hard envelopes | Explicit C-series SKU / TEU-like quantum in Logistics |
| Common lock / handshake | Hub berths, dwell hours, corridor entry | Ship bay lock registry |
| Identification plate | FirmId / shipment ids / ledger memos | Portable container registry + inspection dates |
| Liability portable | Accounting notes, Finance loans, insurance coverages | Bonded freight + salvage claims |
| Not a life-support standard | Cargo SKUs Raw/Capital/Final/Energy only | Passenger / livestock modules forbidden as cargo |

## What is modeled now

- Corridor tolls, transit hours, max cargo, berth capacity
- Active shipments with phases (loading / underway / unloading / waiting berth)
- Firm ledgers: cash, inventory book, revenue/COGS, transport fuel/toll expense, notes
- Core holdings and projected balance sheets at posted prices when present

## What is deliberately not modeled yet

Full C-series length family as first-class cargo objects; machine-readable hazard codes;
passenger-in-container loopholes; campaign-specific advanced pods that bypass the shared
handshake. Those belong in roadmap work once the campaign pulse and Spectre report are solid.
