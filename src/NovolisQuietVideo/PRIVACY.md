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
- It stores only the user's volume, mute, and pre-mute volume controls in private
  app preferences so those controls survive a new video or app restart.
- It does not copy videos to shared storage or create media-library entries.

## File access

Videos are opened through Android's system document picker or an explicit
`ACTION_VIEW` video activation. The player uses the supplied `content://`,
`file://`, or `https://` URI for the current activity session only. Persistable
grants are not requested or retained. When playback is released, the player
drops its in-memory source reference when the activity session ends or the
source is replaced.

The folder action uses Android's tree picker to enumerate direct child
documents for the current session. It keeps only candidate document URIs in
memory, does not retain the folder grant, and does not display, log, or save
filenames. Previous/next transport actions and automatic advance operate only
on that in-memory queue.

The document picker accepts video files and generic attachment documents so a
provider can expose a partial download or an attachment whose MIME type is
`application/octet-stream`. The native decoder decides whether the bytes are
playable. Playback is a compositional native surface: a provider can supply
random-access bytes that wait for missing ranges, which lets P2P or torrent
integrations buffer the container header and next playback window rather than
feeding sparse-file zeros to the decoder. If preparation fails, the player
briefly retries while more provider data may be arriving and also offers a
generic retry action. A source with enough valid data may begin playback; an
incomplete, corrupt, unsupported, or non-video source eventually receives a
generic playback error and is released without being copied or retained.

If native playback cannot open a source, the failure screen may show an
explicit **Open in VLC** action when VLC is installed. This is an opt-in
escape hatch, not part of the quiet-player privacy guarantee: Android and VLC
can receive the current URI and VLC may create its own playback history or
metadata. The app does not launch VLC automatically and does not persist the
URI.

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
