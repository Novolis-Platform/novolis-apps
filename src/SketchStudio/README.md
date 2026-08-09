# Sketch Studio

Freehand / whiteboard product host on [`Novolis.Avalonia.Controls.Sketch`](../../../novolis-avalonia/src/Novolis.Avalonia.Controls.Sketch/README.md).
Ships as WinExe with Inno installer + portable zip from `novolis-apps` releases.

## Documentation

**Start here:** [docs/sketch-studio/getting-started.md](../../docs/sketch-studio/getting-started.md) · [tools](../../docs/sketch-studio/tools.md) · [shortcuts](../../docs/sketch-studio/shortcuts.md)

**Full index:** **[docs/sketch-studio/README.md](../../docs/sketch-studio/README.md)**

In the app: hover any control for a tip; press **F1** for the shortcut dialog.

## Run

```powershell
dotnet run --project d:\novolis\novolis-apps\src\SketchStudio -p:NovolisUseProjectReferences=true
dotnet run --project d:\novolis\novolis-apps\src\SketchStudio -p:NovolisUseProjectReferences=true -- --smoke
```

## Dependencies

`Novolis.Avalonia.Controls`, `Novolis.Avalonia.Controls.Sketch`, Avalonia Desktop / Fluent / Inter, Optris Font Awesome, `Novolis.Avalonia.Packaging.Inno`.

## Related

| Resource | Role |
|----------|------|
| [SketchLab](../../../novolis-dogfooding/apps/avalonia/SketchLab) | Dogfood twin |
| [novolis-apps docs](../../docs/README.md) | Install / release / design map |
