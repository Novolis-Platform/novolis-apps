# Release

`novolis-apps` does not publish NuGet packages. Releases are application binaries distributed via GitHub Releases (installers, portable zips, checksums).

## Versioning

Version `YEAR.MAJOR.MINOR.BUILD` comes from `build/version.json` plus the GitHub Actions `run_number` (via `read-version` in the Merge workflow).

## CI and release assets

Every merge to `main` runs `dotnet build Novolis.Apps.slnx` on Linux, then a Windows release job publishes:

| Asset | Pattern |
|-------|---------|
| Manuscript Studio installer | `ManuscriptStudioSetup-{version}-win-x64.exe` |
| Manuscript Studio portable | `ManuscriptStudio-{version}-win-x64.zip` |
| Concept Studio installer | `ConceptStudioSetup-{version}-win-x64.exe` |
| Concept Studio portable | `ConceptStudio-{version}-win-x64.zip` |
| Checksums | `SHA256SUMS.txt` (all assets above) |

Inno Setup scripts are generated via `Novolis.Avalonia.Packaging.Inno` (`NovolisGenerateInnoScript` MSBuild target). Shared publish logic lives in [`scripts/Publish-NovolisApp.ps1`](../scripts/Publish-NovolisApp.ps1).

## Dependency order

When apps depend on new `Novolis.Avalonia.*` or `Novolis.Rendering.*` APIs, merge and publish upstream repos first, wait for GitHub Packages, then merge **novolis-apps**. Consumers use floating `2026.1.*` versions from GPR only (no local feeds).

Manual republish: run the **Release** workflow (choose `All`, `ManuscriptStudio`, or `ConceptStudio`) or locally:

```powershell
pwsh -File scripts/build-installer.ps1 -App All
```
