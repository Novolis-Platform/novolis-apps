# Release

`novolis-apps` does not publish NuGet packages. Releases are application binaries distributed via GitHub Releases (installers, portable zips, checksums).

## Versioning

Version `YEAR.MAJOR.MINOR.BUILD` comes from `build/version.json` plus the GitHub Actions `run_number` (via `read-version` in the Merge workflow).

## CI and release assets

Every merge to `main` that touches release-impacting paths runs Linux CI first, then a Windows job publishes **all** apps from [`Get-NovolisAppCatalog`](../scripts/Publish-NovolisApp.ps1). Docs-only / markdown pushes are ignored; test-only or non-release paths skip the Windows job.

| Asset | Pattern |
|-------|---------|
| Books Writer Studio installer / zip | `BooksWriterStudioSetup-{version}-win-x64.exe` / `BooksWriterStudio-{version}-win-x64.zip` |
| Draft Studio installer / zip | `DraftStudioSetup-{version}-win-x64.exe` / `DraftStudio-{version}-win-x64.zip` |
| Sketch Studio installer / zip | `SketchStudioSetup-{version}-win-x64.exe` / `SketchStudio-{version}-win-x64.zip` |
| Sins of a Capitalism Tycoon installer / zip | `SinsOfACapitalismTycoonSetup-{version}-win-x64.exe` / `SinsOfACapitalismTycoon-{version}-win-x64.zip` |
| Live Studio installer / zip | `LiveStudioSetup-{version}-win-x64.exe` / `Novolis.Audio.Live.Studio-{version}-win-x64.zip` |
| Checksums | `SHA256SUMS.txt` |

Inno Setup scripts are generated via `Novolis.Avalonia.Packaging.Inno` (`NovolisGenerateInnoScript` MSBuild target):

- **Per-user** install (`PrivilegesRequired=lowest`, `%LocalAppData%\Programs\Novolis\…`) — no admin
- **Publisher** display name `Novolis`; canonical URL [github.com/Novolis-Platform](https://github.com/Novolis-Platform)
- **MIT** license wizard page from repo-root `LICENSE`
- **Brand icon** from repo-root `icon.ico` (`SetupIconFile` + exe `ApplicationIcon`)
- Version resource: Company/Product/Copyright/Description under the Novolis brand

After each successful release, CI keeps the newest **5** GitHub Releases (`scripts/prune-github-releases.ps1`).

## Dependency order

When apps depend on new `Novolis.Avalonia.*` or `Novolis.Rendering.*` APIs, merge and publish upstream repos first, wait for GitHub Packages, then merge **novolis-apps**. Consumers use floating `2026.1.*` versions from GPR only (no local feeds).

Manual republish:

```powershell
pwsh -File scripts/build-installer.ps1 -App All
# or: BooksWriterStudio | DraftStudio | SketchStudio | SinsOfACapitalismTycoon | LiveStudio
```
