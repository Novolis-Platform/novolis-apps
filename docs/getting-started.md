# Getting started

## Prerequisites

- .NET SDK from `global.json` (10.x)
- GitHub Packages auth: `pwsh -File d:\novolis\novolis-governance\scripts\configure-gpr-user-nuget.ps1`
- Optional: Android / MAUI workloads for mobile heads; Inno Setup 6 for local installer compile

## Restore and build one app

```powershell
dotnet restore d:\novolis\novolis-apps\src\ReadAloud\ReadAloud.slnx
dotnet build d:\novolis\novolis-apps\src\ReadAloud\ReadAloud.slnx -c Release
dotnet test d:\novolis\novolis-apps\tests\ReadAloud.Unit\ReadAloud.Unit.csproj -c Release --no-build
```

Desktop run:

```powershell
dotnet run --project d:\novolis\novolis-apps\src\ReadAloud\ReadAloud.Desktop\ReadAloud.Desktop.csproj
```

Android (local):

```powershell
pwsh -File d:\novolis\novolis-apps\scripts\deploy-readaloud-android.ps1
```

## Merglyph (MAUI)

```powershell
dotnet workload install maui-android
dotnet restore d:\novolis\novolis-apps\src\Merglyph\Merglyph.slnx
# Android
dotnet build d:\novolis\novolis-apps\src\Merglyph\Merglyph\Merglyph.csproj -f net10.0-android -p:NovolisMauiTargetFrameworks=net10.0-android
# Windows local only (not a Ship channel)
dotnet build d:\novolis\novolis-apps\src\Merglyph\Merglyph\Merglyph.csproj -f net10.0-windows10.0.19041.0 -p:NovolisMauiTargetFrameworks=net10.0-windows10.0.19041.0
```

Optional Windows file-association registration for local debug:

```powershell
$env:MERGLYPH_REGISTER_FILE_ASSOCIATIONS = '1'
# cleanup:
$env:MERGLYPH_UNREGISTER_FILE_ASSOCIATIONS = '1'
```

## Manifest maintenance

```powershell
pwsh -File d:\novolis\novolis-apps\scripts\verify-apps-manifest.ps1
pwsh -File d:\novolis\novolis-apps\scripts\verify-installer-policy.ps1
pwsh -File d:\novolis\novolis-apps\scripts\verify-android-policy.ps1
```

## Aggregate solution

`Novolis.Apps.slnx` excludes Android/MAUI hosts so it restores on Linux. Prefer per-app solutions for day-to-day work.
