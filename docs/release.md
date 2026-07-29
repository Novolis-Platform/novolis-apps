# Release

`novolis-apps` does not publish NuGet packages. Releases are application binaries distributed via GitHub Releases (installers, portable zips, checksums).

## Versioning

Version `YEAR.MAJOR.MINOR.BUILD` comes from `build/version.json` plus the GitHub Actions `run_number` (via `read-version` in the Merge workflow).

## CI and release assets

Every merge to `main` runs `dotnet build Novolis.Apps.slnx` on Linux, then a Windows release job publishes **all** apps from [`Get-NovolisAppCatalog`](../scripts/Publish-NovolisApp.ps1):

| Asset | Pattern |
|-------|---------|
| Books Writer Studio installer / zip | `BooksWriterStudioSetup-{version}-win-x64.exe` / `BooksWriterStudio-{version}-win-x64.zip` |
| Draft Studio installer / zip | `DraftStudioSetup-{version}-win-x64.exe` / `DraftStudio-{version}-win-x64.zip` |
| Sins of a Capitalism Tycoon installer / zip | `SinsOfACapitalismTycoonSetup-{version}-win-x64.exe` / `SinsOfACapitalismTycoon-{version}-win-x64.zip` |
| Live Studio installer / zip | `LiveStudioSetup-{version}-win-x64.exe` / `Novolis.Audio.Live.Studio-{version}-win-x64.zip` |
| Checksums | `SHA256SUMS.txt` |

Inno Setup scripts are generated via `Novolis.Avalonia.Packaging.Inno` (`NovolisGenerateInnoScript` MSBuild target).

## Dependency order

When apps depend on new `Novolis.Avalonia.*` or `Novolis.Rendering.*` APIs, merge and publish upstream repos first, wait for GitHub Packages, then merge **novolis-apps**. Consumers use floating `2026.1.*` versions from GPR only (no local feeds).

Manual republish:

```powershell
pwsh -File scripts/build-installer.ps1 -App All
# or: BooksWriterStudio | DraftStudio | SinsOfACapitalismTycoon | LiveStudio
```
