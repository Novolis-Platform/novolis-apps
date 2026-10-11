# Novolis Asset Studio

Product host for the host-neutral Visual and Asset Studio packages. The host
composes `Novolis.Avalonia.AssetStudio` and installs the Novolis Graphical
Profile during `Application.Initialize`.

The first slice is intentionally CPU/offline-capable: it opens a deterministic
sample recipe, exposes graph, stack, source, diagnostics, and preview status
surfaces, and does not require a GPU or a preview service.

## Run

```powershell
dotnet run --project d:\novolis\novolis-apps\src\AssetStudio\AssetStudio.csproj -p:NovolisUseProjectReferences=true
```

Type `validate` in the command bar to refresh the shared Visual compiler
diagnostics.
