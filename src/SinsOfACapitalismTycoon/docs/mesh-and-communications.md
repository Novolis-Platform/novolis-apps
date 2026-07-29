# Mesh and communications

Bounded-minimum Confederation **mesh** under [`Universe/Mesh/`](../Universe/Mesh/)
([SPEC.md](../Universe/Mesh/SPEC.md)). Product glue: `MeshBridge` / `MeshPulse`.

Doctrine: [The Confederation Does Not Deliver Messages](https://frankhaugen.github.io/galactic-confederation-review/articles/the-confederation-does-not-deliver-messages/).

## Who has mailboxes

Any endpoint can own a mailbox + feed book, named with a kind prefix:

| Kind | Id factory | Example |
|------|------------|---------|
| Person | `MeshIdentityIds.Person` | `person:ST-7749-…` (James) |
| Household | `MeshIdentityIds.Household` | `household:{guid}` at home system |
| Firm | `MeshIdentityIds.Firm` | `firm:mining` |
| Ship | `MeshIdentityIds.Ship` | `ship:ST Calypso` |
| Thing | `MeshIdentityIds.Thing` | `thing:facility:{guid}` |

## Contract

| Address | Mode | How you get it |
|---------|------|----------------|
| Place (system **node**) | Directed pulse | Visible at destination node cache |
| Identity | Flood | **Push** into mailbox when co-located with a holding node |
| Feed (`News.*`) | Flood | **Pull** only subscribed channels |
| Feed (`Emergency`) | Flood | **Forced** — every mailbox is on it; cannot unsubscribe; force-copied into feed inbox at co-located nodes |

Listening to `News.General` does not pull `News.Prices`. Everyone still gets `Emergency`.

## Campaign seed

Registers person (Calypso master), ships, firms, household cohorts at their systems, and facility things. Smoke publishes include an Emergency alert at Sol.

## Promotion

Extractable to `Novolis.Mesh.Core`. Mesh says **Node**; logistics still says Hub.
