# Terminology

Canonical vocabulary for Sins player glass and new Sins-owned code.

| Domain | Preferred | Ban in UX / new Sins code |
|--------|-----------|---------------------------|
| Docked at infrastructure | **Dock**, **Docked**, **at dock** | Berth (sleeping-bunk sense) |
| Same star system | **Local**, **in system** | — |
| Comms fabric | **Mesh**, **Node**, **Feed**, **Mailbox** | Network (as intel board name) |
| Real space | **System**, **Habitat**, **Station** | Hub (player glass) |

## Intel boards

- **Dock board** — live local tape for the system you are docked in (no FTL lag).
- **Mesh board** — `Commerce.Spot` digests / retractions already in the captain mailbox at the current **node**.

Accept cargo only when **docked** at the load origin (dock act). Seeing an offer on the mesh is not enough.

## Economy package types (unchanged)

`TransportHub`, `TransportHubId`, `ShipmentPhase.WaitingBerth`, `HubOrder` stay Novolis.Economy identifiers. Sins may call those APIs; display strings and Sins-owned names use this glossary.

## Communications

See [mesh-and-communications.md](mesh-and-communications.md). Mesh **Node** ≈ star **System** relay; logistics hubs remain Economy mapping under the bridge.
