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
Feed subscriptions (News.*, Commerce.Spot voluntary; Emergency mandatory)
Packets (pulse | bulk | feed; Subject/Body/Topic; sealed; opaque signature; priority;
        GlobalTtlHours / LocalTtlHours + LocalRetentionPriority)
In-flight drones
Node caches (visibility: PacketId → ReceivedHour + LocalPriority)
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

### Dedup / store

- Canonical body lives once in `Packets[id]`.
- Node caches store membership + `ReceivedHour` + snapped `LocalPriority` — `CreditNode` is idempotent.
- Flood does not launch toward a neighbor that already holds the packet.

### TTL (dual)

| Kind | Clock | Effect |
|------|-------|--------|
| **Global** (`GlobalTtlHours`) | From `PublishedHour` | Earliest universal removal of the packet (all caches, mailboxes, inboxes, drones) |
| **Local** (`LocalTtlHours`) | From node `ReceivedHour` | Drop from that node’s cache only; may reopen flood from neighbors so holes can refill |

**Retraction:** `Topic=spot-retract` + `LogicalKey` floods like a feed; on credit, the node records the key in `NodeRetractions`. Mesh job boards skip retracted keys even if an old digest remains in the feed inbox. Price/qty change ⇒ new logical key; origin must retract the old key (FTL ahead of tramp so you can arrive to find the job gone).

**Local retention priority** (`LocalRetentionPriority`, default = publish priority): when `MaxPacketsPerNodeCache` is set, over-cap nodes drop lowest priority then oldest first. Local time expiry ignores priority (clock wins); priority is for capacity pressure.

Null Global and/or Local = no expiry on that axis. Global floor still applies: the packet object is not deleted before Global even if every local cache has expired.

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
5. `HourAdvance`
6. `TtlExpire` (local then global — after the clock ticks so elapsed hours match TTL)

---

## 5. Promotion / in-app boundary

- **Kernel:** `Universe/Mesh/*.cs` → namespace `…Mesh.Kernel` (internal types).
- **Glue:** `Universe/Mesh/Sins/` → namespace `…Mesh.Sins`.
- Extraction to `Novolis.Mesh.Core` remains future; this boundary is the staging shape.
- Do not put fiction mesh into `Novolis.Transports.*`. Prefer **Node**, not Hub, in mesh terminology
  (Economy `TransportHub` remains a separate logistics concept).
