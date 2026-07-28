# Bounded Minimum Mesh Model

A **bounded minimum mesh model** is the smallest information-propagation model that
preserves Confederation mesh doctrine while excluding operational detail that does not
affect visibility, lag, or capacity questions.

It is **bounded** because it declares what exists, what does not, and where aggregation occurs.

It is **minimum** because each concept must explain a distinct mesh phenomenon.

Doctrine (Galactic Confederation Review — *The Confederation Does Not Deliver Messages*):

- The mesh **publishes and propagates**; it does not guarantee delivery to a person.
- Success = a signed packet becomes **visible** at a hub cache and/or identity mailbox.
- Place addresses use **directed** pulse paths; identity/public use **flood**.
- Pulse drones are tiny, mostly disposable FTL carriers; bulk is slower freight-class hops.

---

## 1. Boundary

```text
Hubs
Edges (hub→hub, pulse vs bulk travel hours)
Identities (optional last-known hub — bias only)
Packets (pulse | bulk | public; sealed; opaque signature; priority; TTL)
In-flight drones (aggregate carriers)
Hub caches (visibility)
Identity mailboxes
Pending launches (bandwidth-gated)
```

**Out of boundary:** real cryptography, ansible, per-drone physics, Economy cash postage,
TCP/HTTP, Limbo ceremony as a special event type, captain UI compose.

---

## 2. Contract

| Address | Propagation | Guarantee |
|---------|-------------|-----------|
| Place(Hub) | Directed hop path | Visible at destination hub eventually (lag/loss may delay) |
| Identity(Id) | Flood among hubs | Visible in mailbox when a hub that holds the packet “connects” identity (mailbox credit on flood arrival at any hub; identity need not be online) |
| Public | Flood | Visible in every hub cache that receives a hop |

There is **no** `IsDeliveredToHuman` API.

---

## 3. Timing

- One `MeshEngine.Advance` = one campaign hour.
- Pulse travel hours = `ceil(ly / PulseLyPerHour)`; default PulseLyPerHour ≈ **20× tramp cruise**
  (`CruiseDaysPerLy = 1.3` ⇒ tramp ~0.032 ly/h; pulse ~0.64 ly/h).
- Bulk travel hours use slower ly/h (near tramp) so layers differ mechanically.
- Hub `PulseBandwidthPerHour` caps new drone launches from that hub each hour.
- Higher `Priority` wins bandwidth contention.

---

## 4. Stocks vs flows

**Stocks:** hubs, edges, packets, caches, mailboxes, identities, in-flight drones, pending launches.

**Flows:** publish, drone hop progress, arrival visibility credit, flood fan-out, loss+requeue, TTL expiry.

---

## 5. Invariants

- Packet ids unique in `Packets`.
- Every in-flight / pending launch references a known packet and hubs.
- Cache/mailbox entries reference known packets.
- Non-negative remaining hours on drones.
- Bandwidth used per hub per hour ≤ `PulseBandwidthPerHour` (enforced at launch).

---

## 6. Period pipeline (`DefaultMeshPipeline`)

1. `DroneTick` — decrement ETAs; apply loss; on arrive credit cache/mailbox; continue directed path or mark flood seed.
2. `FloodDispatch` — from hubs that hold flood/public packets, enqueue neighbor launches (deduped).
3. `LaunchPending` — consume bandwidth by priority; spawn drones.
4. `TtlExpire` — drop expired packets from caches/mailboxes/pending (in-flight may finish).
5. `HourAdvance` — `HourIndex++`, reset per-hour bandwidth counters.

---

## 7. Promotion

This kernel lives under Sins (`Universe/Mesh/`) until lifted to `Novolis.Mesh.Core`.
Product glue stays in `Universe/Mesh/Sins/`. Do not put fiction mesh into `Novolis.Transports.*`.
