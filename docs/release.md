# Release

`novolis-apps` does not publish NuGet packages. Releases are application binaries distributed per-app when needed (installer, GitHub release asset, etc.).

## Versioning

Apps use the repository commit / build date until explicit app versioning is introduced.

## CI

Every merge to `main` runs `dotnet build Novolis.Apps.slnx` via GitHub Actions. No package publish step.
