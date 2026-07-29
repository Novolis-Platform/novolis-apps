# Mesh and communications

Bounded-minimum Confederation **mesh** under [`Universe/Mesh/`](../Universe/Mesh/)
([SPEC.md](../Universe/Mesh/SPEC.md)). Product glue: `MeshBridge` / `MeshPulse` /
`MeshMailboxSync` / `MeshGameplayPulse`.

Doctrine: [The Confederation Does Not Deliver Messages](https://frankhaugen.github.io/galactic-confederation-review/articles/the-confederation-does-not-deliver-messages/).

## Gameplay rule

```text
Same system (dock / co-located node)  → live / push / force Emergency now
Other systems                          → mesh hop delay, then visible / pull
```

- **Dock spot board** — live `BuildSpot` for the current system only.
- **Mesh spot board** — parses `Commerce.Spot` digests already in the captain feed inbox
  (`person:` + `ship:ST Calypso`). Empty until flood + pull catch up (honest lag).
  Lines whose **logical key** is retracted at the current mailbox node are filtered out
  (job taken / price obsolete) — FTL retraction can beat tramp arrival.
- **Escrow** — identity pulses to the carrier ship (and Calypso person); push when co-located.
- **Emergency** — mandatory feed; force into co-located feed inboxes (drama / soft-fail /
  stockout milestones among others).

Packets carry plain-text `Subject` / `Body` / `Topic` (`spot-digest`, `escrow`, `emergency`).

TTL is dual: **Global** (earliest universal removal from publish time) and **Local** (drop from a
node’s cache after that node’s receive time). Local retention has a **priority** used under
per-node cache caps.

## Who has mailboxes

Any endpoint can own a mailbox + feed book, named with a kind prefix:

| Kind | Id factory | Example |
|------|------------|---------|
| Person | `MeshIdentityIds.Person` | `person:ST-7749-…` (James) |
| Household | `MeshIdentityIds.Household` | `household:{guid}` at home system |
| Firm | `MeshIdentityIds.Firm` | `firm:mining` |
| Ship | `MeshIdentityIds.Ship` | `ship:ST Calypso` |
| Thing | `MeshIdentityIds.Thing` | `thing:facility:{guid}` |

Each campaign hour, `MeshMailboxSync` moves ship + player person mailboxes to the hull’s
current system node **before** `MeshPulse.TickHour`.

## Contract

| Address | Mode | How you get it |
|---------|------|----------------|
| Place (system **node**) | Directed pulse | Visible at destination node cache |
| Identity | Flood | **Push** into mailbox when co-located with a holding node |
| Feed (`News.*`, `Commerce.Spot`) | Flood | **Pull** only subscribed channels |
| Feed (`Emergency`) | Flood | **Forced** — every mailbox is on it; cannot unsubscribe; force-copied into feed inbox at co-located nodes |

Listening to `News.General` does not pull `News.Prices`. Everyone still gets `Emergency`.
Calypso person + ship subscribe to `Commerce.Spot` at seed.

## Campaign seed

Registers person (Calypso master), ships, firms, household cohorts at their systems, and facility things. Smoke publishes include an Emergency alert at Sol. Daily `MeshGameplayPulse` publishes spot digests from origin nodes and drains escrow notices.

## Promotion

Extractable to `Novolis.Mesh.Core`. Mesh says **Node**; logistics still says Hub.
