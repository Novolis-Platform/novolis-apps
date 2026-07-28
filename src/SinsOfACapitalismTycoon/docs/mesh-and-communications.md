# Mesh and communications

Bounded-minimum Confederation **mesh** under [`Universe/Mesh/`](../Universe/Mesh/)
([SPEC.md](../Universe/Mesh/SPEC.md)). Product glue: `MeshBridge` / `MeshPulse`.

Doctrine: [The Confederation Does Not Deliver Messages](https://frankhaugen.github.io/galactic-confederation-review/articles/the-confederation-does-not-deliver-messages/).

## Contract

| Address | Mode | Outcome |
|---------|------|---------|
| Place (system hub) | Directed pulse drones along known edges | Packet **visible** at destination hub |
| Identity (ship / person / firm) | Flood among hubs | Packet in **mailbox** (and hub caches along the way) |
| Public | Flood | Visible in hub caches |

There is no delivery-to-human API. Offline identities still accumulate mailbox entries.

## Timing

- One `MeshPulse.TickHour` per campaign hour (after Economy advance).
- Pulse travel ≈ **20× tramp cruise** ly/h (tiny disposable mass).
- Bulk hops use tramp-class ly/h.
- Hub `PulseBandwidthPerHour` gates new launches; higher postage priority wins.

## Campaign wiring

- Seeded from `AstroEconomyBridge` hop graph.
- Smoke publishes at create: directed Sol→Wolf (or first non-Sol) + identity flood for Calypso (`PlayerFlavorId`).
- Spectre report includes a **Mesh** block (hubs, drones, publish/launch stats).

## Promotion

Kernel types are extractable to future `Novolis.Mesh.Core`. Do not promote fiction mesh into `Novolis.Transports.*`.
