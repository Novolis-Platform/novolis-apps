# Presence Ledger

Presence Ledger is a local-first Avalonia application for recording confirmed
arrival and departure at user-defined locations. It stores semantic events,
not a continuous route or GPS breadcrumb history.

## Projects

| Project | Role |
| --- | --- |
| `PresenceLedger.Core` | Pure domain model and deterministic inference |
| `PresenceLedger.Storage` | NDJSON location, state, and event stores |
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
`WifiObservation` values. `PresenceLedger.Core` applies each observation to
each configured location and writes only confirmed `PresenceEvent` values and
restart-safe inference state. Raw observations are not persisted.

The map picker is provider-neutral in `Novolis.Avalonia.Map`. This app supplies
a Kartverket tile/search adapter and displays `© Kartverket` while map content
is visible. Map network access is limited to setup; the ledger itself has no
account, cloud, telemetry, or backend.

## Local validation

From the repository workspace, local cross-repository package builds use
ProjectReference mode:

```powershell
dotnet test d:\novolis\novolis-apps\tests\PresenceLedger.Core.Tests\PresenceLedger.Core.Tests.csproj -c Release -p:NovolisUseProjectReferences=true
dotnet test d:\novolis\novolis-apps\tests\PresenceLedger.Storage.Tests\PresenceLedger.Storage.Tests.csproj -c Release -p:NovolisUseProjectReferences=true
```

Committed builds consume published GitHub Packages `2026.1.*` packages and
nuget.org dependencies.

## Privacy

See [PRIVACY.md](PRIVACY.md), [ANDROID-PERMISSIONS.md](ANDROID-PERMISSIONS.md),
and [ATTRIBUTION.md](ATTRIBUTION.md).
