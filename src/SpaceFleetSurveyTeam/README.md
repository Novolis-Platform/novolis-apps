# Space Fleet: Survey Team

Mobile-native science-fiction survey game: the phone is a live field instrument for mapping and certifying real geographic regions.

Vision: [docs/vision.md](docs/vision.md) · Architecture: [docs/architecture.md](docs/architecture.md)

**Not** in the Windows installer / release catalog — local deploy only (same posture as Books Mobile).

## Projects

| Project | Path | Role |
|---------|------|------|
| `SpaceFleetSurveyTeam` | `SpaceFleetSurveyTeam/` | Shared field shell UI, survey loop stubs |
| `SpaceFleetSurveyTeam.Desktop` | `SpaceFleetSurveyTeam.Desktop/` | Windows head (`WinExe`) |
| `SpaceFleetSurveyTeam.Android` | `SpaceFleetSurveyTeam.Android/` | Android APK (`net10.0-android`, API 23+) |

## Run (Desktop)

```powershell
dotnet run --project d:\novolis\novolis-apps\src\SpaceFleetSurveyTeam\SpaceFleetSurveyTeam.Desktop
```

## Deploy (Android)

```powershell
dotnet build d:\novolis\novolis-apps\src\SpaceFleetSurveyTeam\SpaceFleetSurveyTeam.Android\SpaceFleetSurveyTeam.Android.csproj -c Release
adb install -r <apk-path>
```

Requires the Android workload and a connected device or emulator.

## Platform libraries

Packable instrument gizmos and sensor facades live in platform repos (consume via GitHub Packages `2026.1.*` after publish):

| Sensor / UI | Intended home |
|-------------|----------------|
| Instrument widgets / HUD gizmos | `d:\novolis\novolis-avalonia\src\` (`Novolis.Avalonia.Gaming` and/or `Novolis.Avalonia.Instruments`) |
| On-device GPS, motion, magnetometer | `Novolis.Avalonia.Mobile` + `.Android` / `.Desktop` |
| Microphone analysis primitives | `Novolis.Audio.*` (no retained audio) |
| Host-side ADB tooling | `Novolis.IO.Mobile.Android` — **not** used for on-device survey sensors |

This app stays NuGet-only; no shared libraries under `novolis-apps`.
