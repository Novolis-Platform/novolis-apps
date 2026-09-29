# Presence Ledger

Presence Ledger is a local-first Avalonia application for recording confirmed
arrival and departure at user-defined locations. It keeps sparse, point-in-time
observation samples so each displayed day can be explained and replayed, not a
continuous route or GPS breadcrumb history.

## Product metadata

- Display name: `Presence Ledger`
- Publisher: `Novolis`
- Android application ID: `com.novolis.presenceledger`
- Release channels: Windows installer and portable archive, plus Android APK
- Source license: MIT
- Branding: the Windows window/installer and Android launcher use the shared
  Novolis mark from the repository brand assets.

## Projects

| Project | Role |
| --- | --- |
| `PresenceLedger.Core` | Pure domain model and deterministic inference |
| `PresenceLedger.Storage` | NDJSON location, state, event, and daily observation stores |
| `PresenceLedger.App` | Shared Avalonia UI, map picker, and composition |
| `PresenceLedger.Desktop` | Windows desktop head for setup and history |
| `PresenceLedger.Android` | Android head, permissions, and foreground observation |
| `PresenceLedger.Core.Tests` | Story tests for presence inference |
| `PresenceLedger.Storage.Tests` | Deterministic NDJSON persistence tests |

## Run

```powershell
dotnet run --project src/PresenceLedger/PresenceLedger.Desktop/PresenceLedger.Desktop.csproj
```

The Android head requires the `net10.0-android` workload and a device or
emulator:

```powershell
dotnet build src/PresenceLedger/PresenceLedger.Android/PresenceLedger.Android.csproj -c Release
```

## Architecture

Android supplies sparse location and connected-Wi-Fi evidence. The shared
coordinator converts those readings into `PositionObservation` and
`WifiObservation` values, appends a versioned local observation record, and
then sends the transient observation through the inference engine.
`PresenceLedger.Core` applies each observation to each configured location and
writes confirmed `PresenceEvent` values and restart-safe inference state.
Observation files are append-only NDJSON under:

```text
<app-private-root>/observations/YYYY-MM-DD.ndjson
```

The file name is always the UTC date. The Today view converts the selected
display date into the device timezone, replays the relevant samples, and
shows the resulting segments plus optional evidence details. Location
revisions have effective UTC timestamps so a place can be introduced
retroactively without rewriting existing files.

The map surfaces are provider-neutral in `Novolis.Avalonia.Map`. This app
supplies a Kartverket tile/search adapter and displays `© Kartverket` while
map content is visible. Map tiles and Geonorge address search are the only
network-backed features; the ledger itself has no account, cloud, telemetry,
or backend.

## Local validation

From the repository workspace, local cross-repository package builds use
ProjectReference mode:

```powershell
dotnet test d:\novolis\novolis-apps\tests\PresenceLedger.Core.Tests\PresenceLedger.Core.Tests.csproj -c Release -p:NovolisUseProjectReferences=true
dotnet test d:\novolis\novolis-apps\tests\PresenceLedger.Storage.Tests\PresenceLedger.Storage.Tests.csproj -c Release -p:NovolisUseProjectReferences=true
```

Committed builds consume published GitHub Packages `2026.1.*` packages and
nuget.org dependencies.

## Releases

Release artifacts are published from the `PresenceLedger` choice in the
repository's `Release` workflow. The release includes the Windows installer,
portable archive, Android APK, and `SHA256SUMS.txt`.

## Privacy

See [PRIVACY.md](PRIVACY.md), [ANDROID-PERMISSIONS.md](ANDROID-PERMISSIONS.md),
and [ATTRIBUTION.md](ATTRIBUTION.md).
