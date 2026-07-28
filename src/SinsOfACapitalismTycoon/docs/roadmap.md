# Roadmap (explicitly later)

Out of scope for the campaign-universe v1 plan:

- Live Spectre dashboard pulse loop / mid-tick StarMap animation (Avalonia **post-run briefing room** is shipped)
- Shared campaign library extracted from NearSol dogfood + Sins (full runner/bridge);
  **partial:** hull risk quotes, tape gate pricing, and system role assignment already live in
  Logistics / Markets / Assessment — see [architecture.md](architecture.md)
- Live HYG “N nearest” catalog generation at runtime
- Full C-series container objects and hazard mark enforcement in Logistics
- Portable ship/container registry with inspection dates and salvage claims
- GPR publish choreography for new PackageReferences (CI must publish before
  single-repo PackageReference restores work without ProjectRef)

When adding standards fiction features, prefer **narrow interfaces** (capacity envelopes,
declared ratings, plates) over overfitted cargo pods.

## Recently landed (campaign commerce)

Registry, insurance, FTL profiles, drive life/overhaul, mega bulk lane — plus CCA/Meridian
**commerce teeth**: reputation → MinMargin, Opportunities ugly-standby pool, escrow 5%/10%,
port tiers, jump-band refuse, hull liens, tramp biographies.
Hull quote math / tape gate / `RoleAssigner` promoted into Logistics, Markets, and Assessment.
See [ship-law-and-transit.md](ship-law-and-transit.md) and [commerce-stack.md](commerce-stack.md).

Still later: passenger cells, piracy, rescue maturity, full C-series objects.
