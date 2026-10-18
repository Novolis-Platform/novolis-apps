# nMule privacy notes

nMule does not require a Novolis account and does not include analytics,
advertising, relay, telemetry, or cloud storage.

The app stores editable `server.met` and `nodes.dat` bootstrap caches plus
completed files in an app-private profile directory. On Android, that is the
application data directory. On Windows, that is nMule's per-user local
application-data directory. The Windows build does not use public/shared
storage, register file associations, or import files from other applications.
Uninstall retention follows the host platform; remove the app-private nMule
directory separately when its cached or completed files should also be removed.

When the VPN gate is open, nMule connects to the configured HTTPS bootstrap
source and to eD2K/Kad peers selected by the protocol. Search terms and file
metadata are sent to the selected eD2K server. The app does not enable
cleartext HTTP. Android and Windows deny Tailscale unless the user explicitly
enables the session-only Tailscale override. Both hosts also expose a complete,
session-only VPN requirement override. It is off by default, is not persisted,
is not recommended, and permits ordinary network paths as well as Tailscale.

Windows VPN detection is based on active tunnel/PPP adapters, conservative
adapter-name and description signals, tunnel addresses, and Quad100 DNS.
Windows does not provide a universal public API that identifies the owning VPN
application, so an unusual VPN may remain blocked until the complete session
override is enabled. Inspection failures remain blocked unless that override
is active.

No transfer runs in the background in this MVP. Users remain responsible for
the files they search for, retrieve, and share through the selected network.
