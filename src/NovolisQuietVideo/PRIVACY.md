# Novolis Quiet Video privacy contract

Novolis Quiet Video is a foreground-only Android video player. It opens local
files, provider-backed attachments, and user-selected HTTPS sources while
minimizing disclosure about the video being played.

## What the app does not do

- It has no accounts, telemetry, analytics, or cloud sync.
- It uses network access only when the user supplies an HTTPS video source or
  selects a network-backed document provider. The app does not contact a fixed
  service or discover network media on its own.
- It does not enumerate or query `MediaStore`.
- It does not request storage or `READ_MEDIA_VIDEO` permissions.
- It does not publish a `MediaSession`, notification, lock-screen control, or
  Bluetooth playback metadata.
- It does not persist a selected URI, filename, playback history, thumbnail,
  title, artist, artwork, duration, or diagnostic log.
- It does not copy videos to shared storage or create media-library entries.

## File access

Videos are opened through Android's system document picker or an explicit
`ACTION_VIEW` video activation. The player uses the supplied `content://`,
`file://`, or `https://` URI for the current activity session only. Persistable
grants are not requested or retained. When playback is released, the player
drops its in-memory source reference.

The document picker accepts video files and generic attachment documents so a
provider can expose a partial download or an attachment whose MIME type is
`application/octet-stream`. The native decoder decides whether the bytes are
playable. A source with enough valid data may begin playback; an incomplete,
corrupt, unsupported, or non-video source receives a generic playback error and
is released without being copied or retained.

HTTPS servers and document providers can observe the request, source URL,
account context, and network address according to their own policies. The app
does not log or display the URL. Cleartext HTTP is not enabled.

## Foreground-only behavior

The player pauses when its activity loses focus. It does not run a background
service, acquire a wake lock, or continue playback while another activity is
visible. This keeps source details out of notification and system media-control
surfaces.

## Limits of the privacy guarantee

Android can observe that this application is running, that the system picker or
an external video activation was used, and that the app made a user-requested
network connection. The selected document provider can observe URI access
according to its own policy. A source that was already indexed by Android or
another application cannot be un-indexed by this player. These limits are why
the app promises minimized disclosure rather than absolute invisibility.
