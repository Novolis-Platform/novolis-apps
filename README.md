# novolis-apps

Production desktop applications built exclusively from **NuGet packages** (`PackageReference` to `Novolis.*` on GitHub Packages). No in-repo shared libraries — each app under `src/` is a complete project.

## Quick start

```powershell
git clone https://github.com/Novolis-Platform/novolis-apps.git
cd novolis-apps
..\novolis-governance\scripts\configure-gpr-user-nuget.ps1
dotnet restore
dotnet build --no-restore
dotnet run --project src/DraftStudio
```

## Releases

Every successful merge to `main` (non-doc paths) publishes a [GitHub Release](https://github.com/Novolis-Platform/novolis-apps/releases) with portable zips + Inno installers for **every** catalog app, plus `SHA256SUMS.txt`.

| App | Installer | Portable zip |
|-----|-----------|--------------|
| Books Writer Studio | `BooksWriterStudioSetup-{version}-win-x64.exe` | `BooksWriterStudio-{version}-win-x64.zip` |
| Draft Studio | `DraftStudioSetup-{version}-win-x64.exe` | `DraftStudio-{version}-win-x64.zip` |
| Novolis CAD Studio 3D | `CadStudio3DSetup-{version}-win-x64.exe` | `CadStudio3D-{version}-win-x64.zip` |
| Sketch Studio | `SketchStudioSetup-{version}-win-x64.exe` | `SketchStudio-{version}-win-x64.zip` |
| Sins of a Capitalism Tycoon | `SinsOfACapitalismTycoonSetup-{version}-win-x64.exe` | `SinsOfACapitalismTycoon-{version}-win-x64.zip` |
| Live Studio | `LiveStudioSetup-{version}-win-x64.exe` | `Novolis.Audio.Live.Studio-{version}-win-x64.zip` |

Version format: `YEAR.MAJOR.MINOR.BUILD` from `build/version.json` plus CI build number (e.g. `2026.1.0.42`).

Manual republish: run the **Release** workflow from Actions (All or one app), or locally:

```powershell
pwsh -File scripts/build-installer.ps1 -App All
```

## Apps

| App | Path | Description |
|-----|------|-------------|
| Books Writer Studio | `src/BooksWriterStudio` | Three-column book authoring: chapter nav, markdown editor, metadata/publish/SCM |
| Books Mobile | `src/BooksMobile` | Avalonia Android + Windows markdown editor for `frankhaugen/books` (local deploy only — not released) |
| Space Fleet: Survey Team | `src/SpaceFleetSurveyTeam` | Mobile field-instrument survey game (local deploy only — not released) |
| Draft Studio | `src/DraftStudio` | Command-driven 2D/3D CAD-light (`.cadjson` + phys export) |
| Novolis CAD Studio 3D | `src/CadStudio3D` | Full 2D/3D CAD + scene staging, materials, lit render; dual Cad/Scene agent surfaces |
| Sketch Studio | `src/SketchStudio` | Freehand sketch studio (`SketchControl`, `.sketchjson`, PNG/SVG clipboard) |
| Sins of a Capitalism Tycoon | `src/SinsOfACapitalismTycoon` | Headless/Avalonia BM economy sim (`Novolis.Economy.Core`) |
| Capitalist Simulator | `src/CapitalistSimulator` | Capitalism 2 homage firm/unit firm sim (local only — not released) |
| Live Studio | `src/LiveStudio` | Avalonia demo for Novolis Audio Live (host + launcher + studio; DSL editor + visuals) |

## Related

- [docs/design.md](docs/design.md)
- [docs/release.md](docs/release.md)
- [nuget-only-policy](https://github.com/Novolis-Platform/novolis-governance/blob/main/docs/nuget-only-policy.md)
