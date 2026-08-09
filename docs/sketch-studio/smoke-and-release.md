# Smoke and release

← [Documentation index](README.md) · [Architecture](architecture.md) · [Getting started](getting-started.md)

## `--smoke`

Headless pipeline (no main window). Invoked before Avalonia desktop lifetime:

```powershell
dotnet run --project d:\novolis\novolis-apps\src\SketchStudio -p:NovolisUseProjectReferences=true -- --smoke
```

`SmokeRunner` checks:

1. Gridify + undo on a stroke
2. Fuse two strokes (shared `groupId`)
3. `SketchJson` round-trip (v2 text + rotation + group)
4. `SketchExport.ToSvg` non-empty
5. Avalonia `SetupWithoutStarting` + `ToPng` byte length

Exit code `0` on pass, `1` on any failure. Printed as `OK` / `FAIL` lines.

## Local installer

Requires Inno Setup 6 for the setup exe:

```powershell
pwsh -File d:\novolis\novolis-apps\scripts\build-installer.ps1 -App SketchStudio
```

Zip-only:

```powershell
pwsh -File d:\novolis\novolis-apps\scripts\build-installer.ps1 -App SketchStudio -SkipInstaller
```

Catalog entry (`Publish-NovolisApp.ps1`): key `sketch-studio`, AppId `Novolis.SketchStudio`, SetupBase `SketchStudioSetup`, script `sketch-studio.iss`.

## Release assets

| Asset | Role |
|-------|------|
| `SketchStudioSetup-{version}-win-x64.exe` | User-scoped Inno install |
| `SketchStudio-{version}-win-x64.zip` | Portable |
| `SHA256SUMS.txt` | Hashes on the GitHub Release |

Apps docs: [release.md](../release.md), [getting-started.md](../getting-started.md).

## Policy checks

```powershell
pwsh -File d:\novolis\novolis-governance\scripts\verify-nuget-only.ps1
```

## Manual smoke (UI)

After a UI change, verify: draw → save → reopen → Recent → Fuse → Copy PNG → Save As SVG → dirty close → **F1** tips still match [shortcuts](shortcuts.md).
