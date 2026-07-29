# Bounded Minimum Mesh Model

A **bounded minimum mesh model** is the smallest information-propagation model that
preserves Confederation mesh doctrine while excluding operational detail that does not
affect visibility, lag, or capacity questions.

Doctrine (Galactic Confederation Review — *The Confederation Does Not Deliver Messages*):

- The mesh **publishes and propagates**; it does not guarantee delivery to a person.
- Success = a signed packet becomes **visible** at a node cache, and/or reaches a mailbox / feed inbox under the rules below.
- Place addresses use **directed** pulse paths; identity and feeds use **flood** among nodes.
- Pulse drones are tiny, mostly disposable FTL carriers; bulk is slower freight-class hops.

---

## 1. Boundary

```text
Nodes (star-system relays)
Edges (node→node, pulse vs bulk travel hours)
Mailboxes (person | household | firm | ship | thing + location node)
Feed subscriptions (News.* voluntary; Emergency mandatory)
Packets (pulse | bulk | feed; sealed; opaque signature; priority; TTL)
In-flight drones
Node caches (visibility)
Mailbox pushed packets (private identity mail)
Feed inboxes (pulled + forced Emergency)
Pending launches (bandwidth-gated)
```

**Out of boundary:** real cryptography, ansible, per-drone physics, Economy cash postage,
TCP/HTTP, Limbo ceremony, captain UI compose.

---

## 2. Contract

| Address | Propagation | How you receive it |
|---------|-------------|--------------------|
| Place(Node) | Directed hop path | Visible at destination **node** cache |
| Identity(Id) | Flood among nodes | **Push** into that identity's **mailbox** only while the mailbox is co-located with a node that holds the packet |
| Feed(FeedId) | Flood among nodes | **Pull** subscribed channels into feed inbox. `News.General` ≠ `News.Prices`. |
| Feed(`Emergency`) | Flood among nodes | **Mandatory**: every mailbox is subscribed; cannot unsubscribe; force-delivered into feed inboxes of all mailboxes co-located with a node that holds it |

There is **no** `IsDeliveredToHuman` API.

### Mailbox owners

Use `MeshIdentityIds` (`person:`, `household:`, `firm:`, `ship:`, `thing:`) and `MeshIdentityKind` on `MeshMailbox`.

### Mailbox push

A mailbox is parked at a star system (`LocationNodeId`). When an identity-addressed packet becomes visible at that node, it is pushed into the mailbox. Move the mailbox (ship jumps) and catch-up push runs at the new node (including pending Emergency).

### Feed pull (Atom/RSS-ish) + Emergency

Voluntary feed packets populate node caches and are pulled only if subscribed. `Emergency` is always in the effective subscription set and is also **forced** into co-located feed inboxes as soon as the packet is visible at the node (`FeedEngine.ForceMandatoryAtNode`).

---

## 3. Timing

- One `MeshEngine.Advance` = one campaign hour.
- Pulse travel hours = `ceil(ly / PulseLyPerHour)`; default ≈ **20× tramp cruise**.
- Bulk travel hours use slower ly/h.
- Node `PulseBandwidthPerHour` caps new drone launches; higher priority wins.

---

## 4. Period pipeline (`DefaultMeshPipeline`)

1. `DroneTick`
2. `FloodDispatch` (identity + feed)
3. `LaunchPending`
4. `FeedPullAll` (subscribed channels at each mailbox location)
5. `TtlExpire`
6. `HourAdvance`

---

## 5. Promotion

Kernel under Sins (`Universe/Mesh/`) until `Novolis.Mesh.Core`. Glue in `Universe/Mesh/Sins/`.
Do not put fiction mesh into `Novolis.Transports.*`. Prefer **Node**, not Hub, in mesh terminology
(Economy `TransportHub` remains a separate logistics concept).
