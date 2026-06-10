# Release

`novolis-apps` does not publish NuGet packages. Releases are application binaries distributed via GitHub Releases (installer, portable zip, checksums).

## Versioning

Version `YEAR.MAJOR.MINOR.BUILD` comes from `build/version.json` plus the GitHub Actions `run_number` (via `read-version` in the Merge workflow).

## CI and release assets

Every merge to `main` runs `dotnet build Novolis.Apps.slnx` on Linux, then a Windows release job publishes:

| Asset | Pattern |
|-------|---------|
| Installer | `ManuscriptStudioSetup-{version}-win-x64.exe` |
| Portable zip | `ManuscriptStudio-{version}-win-x64.zip` |
| Checksums | `SHA256SUMS.txt` |

Inno Setup scripts are generated via `Novolis.Avalonia.Packaging.Inno` (`NovolisGenerateInnoScript` MSBuild target).

## Dependency order

When Manuscript Studio depends on new `Novolis.Avalonia.*` APIs, merge and publish **novolis-avalonia** first, wait for GitHub Packages to contain the new build, then merge **novolis-apps**. Consumers use floating `2026.1.*` versions from GPR only (no local feeds).
