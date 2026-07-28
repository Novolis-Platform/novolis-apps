# Universe

## Catalog

Canonical seed: **100** Johnston-based near-Sol stars (same slice as the NearSol dogfood app),
embedded as `data/nearsol-100.json`. Sorted by distance from origin; Sol must be present.

This is not a live “N nearest from HYG” query at runtime. Provenance stays fixed so
determinism and CI smokes remain boring.

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

Travel constant (v1): **1.3 days per light-year** on corridors (inherited from dogfood tuning).
