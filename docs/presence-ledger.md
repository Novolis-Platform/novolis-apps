# Presence Ledger

Presence Ledger is the `avalonia-mobile` product in `src/PresenceLedger`.
Its durable output is a small local ledger of confirmed arrival and departure
events at user-defined locations, plus UTC-daily raw samples used to explain
and replay each displayed day.

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
markers, radius and track overlays, selection, accessible zoom, loading/error
state, and attribution. The app owns the Kartverket adapter.
`Novolis.Avalonia.Mobile` owns neutral location and connected-Wi-Fi reading
contracts; its Android package owns Android API adapters. Presence inference,
observation retention, and day projection remain in the app's testable core.

## Data boundary

Platform readings are accepted into local versioned NDJSON files under
`observations/YYYY-MM-DD.ndjson`, partitioned by UTC date. The core also
persists configured location revisions, inference state needed across process
death, and semantic `PresenceEvent` records. NDJSON is the current store; the
contracts leave room for a later SQLite implementation without changing the
engine or day projection.

Map tiles and explicit address searches may use Kartverket/Geonorge over
HTTPS. No ledger data is uploaded.

See the product-level details in
[src/PresenceLedger/README.md](../src/PresenceLedger/README.md).
