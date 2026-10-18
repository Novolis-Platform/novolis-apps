# nMule privacy notes

nMule does not require a Novolis account and does not include analytics,
advertising, relay, or cloud storage.

The app stores editable `server.met` and `nodes.dat` bootstrap caches plus
completed files in its Android app-private profile directory. Android removes
that data when the app is uninstalled unless the platform's restore behavior
is changed by the user.

When the VPN gate is open, nMule connects to the configured HTTPS bootstrap
source and to eD2K/Kad peers selected by the protocol. Search terms and file
metadata are sent to the selected eD2K server. The app does not enable
cleartext HTTP. Tailscale is denied unless the user explicitly enables the
session-only override.

No transfer runs in the background in this MVP. Users remain responsible for
the files they search for, retrieve, and share through the selected network.
