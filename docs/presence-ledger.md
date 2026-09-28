# Presence Ledger

Presence Ledger is the `avalonia-mobile` product in `src/PresenceLedger`.
Its durable output is a small local ledger of confirmed arrival and departure
events at user-defined locations.

## Composition

The app composes general-purpose platform libraries rather than putting
Android or map concerns into the domain:

```text
Novolis.Math.Geometry
  -> PresenceLedger.Core
  -> PresenceLedger.Storage
  -> PresenceLedger.App
  -> PresenceLedger.Desktop / PresenceLedger.Android
```

`Novolis.Avalonia.Map` owns provider-neutral map viewport behavior, tiles,
markers, radius overlays, selection, and attribution. The app owns the
Kartverket adapter. `Novolis.Avalonia.Mobile` owns neutral location and
connected-Wi-Fi reading contracts; its Android package owns Android API
adapters. Presence inference remains in the app's testable core.

## Data boundary

Platform readings are transient. The core persists only configured locations,
inference state needed across process death, and semantic `PresenceEvent`
records. NDJSON is the v1 store; the contracts leave room for a later SQLite
implementation without changing the engine.

See the product-level details in
[src/PresenceLedger/README.md](../src/PresenceLedger/README.md).
