# Presence Ledger privacy model

Presence Ledger is designed to keep personal presence history on the device.

## Stored locally

- Locations explicitly configured by the user.
- Wi-Fi names explicitly configured by the user.
- Inferred `Arrived` and `Left` events.
- Restart-safe inference state needed to finish a pending decision.
- A private map tile cache used during setup.

## Not intentionally retained

- Continuous movement history.
- Routes, breadcrumbs, or every GPS fix.
- Nearby Wi-Fi inventories or scan results.
- Cloud copies, accounts, analytics, advertising identifiers, or telemetry.
- Raw location and Wi-Fi observations after they have been offered to the
  inference engine.

The application prefers a missing event over a false event. If a platform
permission, location service, or background execution mode is unavailable,
the app reports degraded capability and waits rather than fabricating evidence.

The map picker may contact the Kartverket and Geonorge endpoints listed in the
app catalog. Those requests are setup infrastructure; presence history is not
uploaded.
