# Presence Ledger privacy model

Presence Ledger is designed to keep personal presence history on the device.

## Stored locally

- Locations explicitly configured by the user.
- Wi-Fi names explicitly configured by the user.
- Inferred `Arrived` and `Left` events.
- Restart-safe inference state needed to finish a pending decision.
- Versioned sparse observations under `observations/YYYY-MM-DD.ndjson`,
  partitioned by UTC date. Records contain the accepted UTC timestamp,
  optional position and accuracy, Wi-Fi capability status, and optional
  connected SSID.
- A private map tile cache used during setup.

## Not retained as a route

- Presence Ledger does not retain a continuous movement route or every GPS fix
  as a navigable breadcrumb trail. Samples are retained only to make local
  diagnostics, replay, and day projection explainable.
- Nearby Wi-Fi inventories or scan results.
- Cloud copies, accounts, analytics, advertising identifiers, or telemetry.

Raw observation files are local debug data. They can be inspected or removed
from the app-private storage by the user; the app does not upload them.

The application prefers a missing event over a false event. If a platform
permission, location service, or background execution mode is unavailable,
the app reports degraded capability and waits rather than fabricating evidence.

The Today map and location editor may contact the Kartverket tile and Geonorge
address-search endpoints listed in the app catalog. Those requests carry map
viewport/search information only; presence history, configured locations, and
raw observation files are not uploaded. A failed network request must leave
the local day view and local editing data intact.
