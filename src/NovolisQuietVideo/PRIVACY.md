# Novolis Quiet Video privacy contract

Novolis Quiet Video is an offline, foreground-only Android video player. It is
designed to minimize disclosure about the video being played.

## What the app does not do

- It has no accounts, telemetry, analytics, network access, or cloud sync.
- It does not enumerate or query `MediaStore`.
- It does not request storage or `READ_MEDIA_VIDEO` permissions.
- It does not publish a `MediaSession`, notification, lock-screen control, or
  Bluetooth playback metadata.
- It does not persist a selected URI, filename, playback history, thumbnail,
  title, artist, artwork, duration, or diagnostic log.
- It does not copy videos to shared storage or create media-library entries.

## File access

Videos are opened through Android's system document picker or an explicit
`ACTION_VIEW` video activation. The player uses the granted `content://` or
`file://` URI for the current activity session only. Persistable grants are not
requested or retained. When playback is released, the player drops its
in-memory source reference.

`EXTRA_LOCAL_ONLY` is supplied to the document picker as a preference. Android
providers may still expose virtual or cloud-backed documents, and the player
cannot independently prove where a provider stores a document.

## Foreground-only behavior

The player pauses when its activity loses focus. It does not run a background
service, acquire a wake lock, or continue playback while another activity is
visible. This keeps source details out of notification and system media-control
surfaces.

## Limits of the privacy guarantee

Android can observe that this application is running and that the system
picker or an external video activation was used. The selected document
provider can observe URI access according to its own policy. A source that was
already indexed by Android or another application cannot be un-indexed by this
player. These limits are why the app promises minimized disclosure rather than
absolute invisibility.
