# novolis-apps

Production desktop applications built exclusively from **NuGet packages** (`PackageReference` to `Novolis.*` on GitHub Packages). No in-repo shared libraries — each app under `src/` is a complete project.

## Quick start

```powershell
git clone https://github.com/Novolis-Platform/novolis-apps.git
cd novolis-apps
..\novolis-governance\scripts\configure-gpr-user-nuget.ps1
dotnet restore
dotnet build --no-restore
dotnet run --project src/ManuscriptStudio
```

## Releases

Every successful merge to `main` (non-doc paths) publishes a [GitHub Release](https://github.com/Novolis-Platform/novolis-apps/releases) with:

- `ManuscriptStudioSetup-{version}-win-x64.exe` — Manuscript Studio installer (per-user, no admin)
- `ManuscriptStudio-{version}-win-x64.zip` — Manuscript Studio portable publish folder
- `ConceptStudioSetup-{version}-win-x64.exe` — Concept Studio installer
- `ConceptStudio-{version}-win-x64.zip` — Concept Studio portable publish folder
- `SHA256SUMS.txt` — checksums for all release assets

Version format: `YEAR.MAJOR.MINOR.BUILD` from `build/version.json` plus CI build number (e.g. `2026.1.0.42`).

Manual republish: run the **Release** workflow from Actions, or locally:

```powershell
pwsh -File scripts/build-installer.ps1
```

## Apps

| App | Path | Description |
|-----|------|-------------|
| Manuscript Studio | `src/ManuscriptStudio` | Markdown editor with Generic + Book Authoring + Concept Assets modes |
| Concept Studio | `src/ConceptStudio` | Simple 3D concept CAD for ship/prop blockout, ortho views, SVG/PNG export |

## Related

- [docs/design.md](docs/design.md)
- [nuget-only-policy](https://github.com/Novolis-Platform/novolis-governance/blob/main/docs/nuget-only-policy.md)
