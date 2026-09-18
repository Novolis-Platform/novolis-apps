<!-- novolis-marketing:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-brand-transparent.svg" width="360" alt="Novolis"/>
  </a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/banners/novolis-apps.svg" width="100%" alt="novolis-apps"/>
</p>

<p align="center">
  <strong>Desktop products on NuGet only</strong><br/>
  Production Avalonia apps and installers composed entirely from Novolis packages.
</p>

<p align="center">
  <a href="https://novolis-platform.github.io/.github/novolis-apps/"><img src="https://img.shields.io/badge/docs-portfolio-0a7ea3" alt="docs"/></a>
  <a href="https://github.com/Novolis-Platform/novolis-apps/actions"><img src="https://img.shields.io/github/actions/workflow/status/Novolis-Platform/novolis-apps/merge.yml?branch=main&label=merge&logo=github" alt="merge"/></a>
  <a href="https://github.com/orgs/Novolis-Platform/packages?repo_name=novolis-apps"><img src="https://img.shields.io/badge/packages-GitHub%20Packages-0a7ea3?logo=nuget" alt="packages"/></a>
  <a href="https://github.com/Novolis-Platform"><img src="https://img.shields.io/badge/org-Novolis--Platform-111827" alt="org"/></a>
</p>

<p align="center">
  <a href="https://novolis-platform.github.io/.github/novolis-apps/">Docs</a>
  ·
  <a href="https://nuget.pkg.github.com/Novolis-Platform/index.json"><code>https://nuget.pkg.github.com/Novolis-Platform/index.json</code></a>
  ·
  <a href="https://github.com/Novolis-Platform/.github/blob/main/profile/README.md">Org landing</a>
  ·
  <a href="https://github.com/Novolis-Platform/novolis-governance">Governance</a>
</p>

---
<!-- novolis-marketing:end -->
# novolis-apps

Production applications built exclusively from **NuGet packages** (`PackageReference` to `Novolis.*` on GitHub Packages). Authoritative catalog: [`build/apps.json`](build/apps.json). Every `src/` tree declares at least one **Ship** channel — there is no empty-Ship / “internal only” holding state.

## Quick start (one app)

```powershell
pwsh -File d:\novolis\novolis-governance\scripts\configure-gpr-user-nuget.ps1
dotnet restore d:\novolis\novolis-apps\src\DraftStudio\DraftStudio.slnx
dotnet build d:\novolis\novolis-apps\src\DraftStudio\DraftStudio.slnx --no-restore
dotnet run --project d:\novolis\novolis-apps\src\DraftStudio\DraftStudio.csproj
```

Regenerate solutions after editing the manifest:

```powershell
dotnet run --project d:\novolis\novolis-apps\tools\AppsManifest\AppsManifest.csproj -- generate-solutions --repo d:\novolis\novolis-apps
```

`Novolis.Apps.slnx` is a Linux-safe aggregate for local discovery only — not the default CI/release graph.

## Local versus Ship

| | Meaning |
|---|---|
| **Local** | Platforms you can restore/build/debug (may include Linux or Windows MAUI without shipping them) |
| **Ship** | Release channels that produce artifacts (`windows-inno`, `android-apk`) |

Phase one does **not** ship Linux installers. Linux remains a local/PR capability where the stack supports it.

## Releases

PR/merge CI validates **changed apps only** (scoped solutions + tests; Android compile when declared). Packaging is **manual** via the **Release** workflow: select an app and channel from fixed dropdowns.

| Channel | Artifacts |
|---------|-----------|
| `windows-inno` | Per-user Inno under `%LocalAppData%\Programs\Novolis\…` + one installer `.exe` per app + SHA-256 |
| `android-apk` | Signed APK + SHA-256 (persistent keystore required) |

Version format: `YEAR.MAJOR.MINOR.BUILD` from `build/version.json` plus workflow run number.

```powershell
pwsh -File d:\novolis\novolis-apps\scripts\build-installer.ps1 -App DraftStudio
```

## Apps (from manifest)

| App | Stack | Ship | Local |
|-----|-------|------|-------|
| Books Writer Studio | avalonia-desktop | windows-inno | windows, linux |
| Draft Studio | avalonia-desktop | windows-inno | windows, linux |
| Novolis CAD Studio 3D | avalonia-desktop | windows-inno | windows, linux |
| Sketch Studio | avalonia-desktop | windows-inno | windows, linux |
| Sins of a Capitalism Tycoon | avalonia-desktop | windows-inno | windows, linux |
| Live Studio | avalonia-desktop | windows-inno | windows, linux |
| Books Mobile | avalonia-mobile | windows-inno, android-apk | windows, linux, android |
| Read Aloud | avalonia-mobile | windows-inno, android-apk | windows, linux, android |
| Coverage Studio | avalonia-desktop | windows-inno | windows, linux |
| Capitalist Simulator | avalonia-desktop | windows-inno | windows, linux |
| GeoPolity | avalonia-desktop | windows-inno | windows, linux |
| Repo Studio | avalonia-desktop | windows-inno | windows, linux |
| Ship Designer | avalonia-desktop | windows-inno | windows, linux |
| Space Fleet: Survey Team | avalonia-mobile | windows-inno | windows, linux, android |
| Merglyph | maui | android-apk | windows, android |

List programmatically: `dotnet run --project d:\novolis\novolis-apps\tools\AppsManifest\AppsManifest.csproj -- list`

## Related

- [docs/design.md](docs/design.md)
- [docs/release.md](docs/release.md)
- [docs/getting-started.md](docs/getting-started.md)
- [installer-data-lifecycle](https://github.com/Novolis-Platform/novolis-governance/blob/main/docs/installer-data-lifecycle.md)
- [nuget-only-policy](https://github.com/Novolis-Platform/novolis-governance/blob/main/docs/nuget-only-policy.md)
