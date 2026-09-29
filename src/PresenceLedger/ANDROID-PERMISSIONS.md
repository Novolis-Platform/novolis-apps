# Android permissions and lifecycle

Presence Ledger requests only the platform access needed to infer presence at
locations the user configured.

| Permission | Purpose |
| --- | --- |
| `INTERNET` | Download map tiles and address-search results during setup |
| `ACCESS_COARSE_LOCATION` / `ACCESS_FINE_LOCATION` | Produce sparse position evidence |
| `ACCESS_BACKGROUND_LOCATION` | Continue observation when the app surface is not visible |
| `NEARBY_WIFI_DEVICES` | Read the currently connected Wi-Fi on modern Android |
| `FOREGROUND_SERVICE` / `FOREGROUND_SERVICE_LOCATION` | Run the low-frequency observation service |
| `POST_NOTIFICATIONS` | Show the required foreground-service notification on supported Android versions |

The app does not scan nearby networks. It reads only the connected network
when Android permits it. Location permission is also required because Android
can redact Wi-Fi identity without location access.

The activity requests foreground permissions first and then requests background
location separately, following Android's staged permission rules. If background
access is denied, the diagnostics page reports the resulting limitation.

Once foreground location permission is available, the Android head starts a
low-priority foreground service. The service owns no inference logic: it
starts the shared observation coordinator, which appends each accepted sample
to the app-private UTC daily NDJSON store and sends the transient reading
through a channel to `PresenceLedger.Core`. Stopping the service cancels the
source subscriptions; it does not send data anywhere.

Backup is disabled and cleartext network traffic is disabled. Map traffic is
HTTPS-only and is limited to the Kartverket/Geonorge destinations declared in
`build/apps.json`.
