# Universe

## Catalog

Canonical seed: **100** Johnston-based near-Sol stars via Astro
`CatalogPacks.NearSol100` (`Novolis.Astro.Catalog.Data`) — same pack as dogfood /
Astro unit smokes. Sol must be present. No local JSON fork under `Universe/data/`.

This is not a live “N nearest from HYG” query at runtime. Provenance stays fixed so
determinism and CI smokes remain boring. Refresh the pack in Astro (`tools/pregen-catalog.cs`),
not by editing Sins.

## Hop graph

- Max range: **12 ly**
- Cost model: Astro `RangeBandCostModel` prototype-compatible
- Connected systems become Economy **transport hubs**; undirected edges become bidirectional
  **corridors** (travel days/ly, toll/ly, max cargo)

Isolated Sol is forced onto the hub set if the graph would leave it out.

## Roles (potential-gated)

Assigned after profiles: Capital (Sol), Inhabited, Industrial, Mining, Transit, Waypoint —
with caps similar to the dogfood baseline (e.g. Mining ≤10, Industrial ≤5, Transit ≤20). Roles drive
facility placement and agent homes, not pretty labels alone.

## Economy geography

Each hub binds to:

- Inventory locations (storage / retail as seeded)
- Optional Core region mapping via bridge (`HubRegions`)
- Cohorts and habitats where inhabited

Travel constant (v1): **1.3 days per light-year** on corridors (dogfood tuning).
Calypso jump brief for flavor: **ten ly routine, twelve hurts** — see
[commerce-stack.md](commerce-stack.md) and [ship-law-and-transit.md](ship-law-and-transit.md).
Station *tiers* (Duckville → Tortuga) overlay roles without renaming stars —
[places-and-stations.md](places-and-stations.md).
