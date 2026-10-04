# Reach.Protocol

Product-private Reach wire types. One assembly; folders are the namespaces.

| Folder | Namespace | Contents |
|---|---|---|
| *(root)* | `Novolis.Reach.Protocol` | App id, version, ports, JSON options |
| `Framing/` | `.Framing` | Envelope, codec, channel, message kind |
| `Session/` | `.Session` | Hello/open/resume/close, capabilities, state machine |
| `Input/` | `.Input` | Displays, pointer, keys, clipboard |
| `Media/` | `.Media` | Video, audio, datagram offer |
| `Transfer/` | `.Transfer` | File offer/chunk/complete |
| `Host/` | `.Host` | Operator commands, status, latency, metrics snapshot |

Not a NuGet package. Do not split these into extra projects — they are one codec.
