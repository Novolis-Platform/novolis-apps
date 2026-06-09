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

## Apps

| App | Path | Description |
|-----|------|-------------|
| Manuscript Studio | `src/ManuscriptStudio` | Markdown editor with Generic + Book Authoring built-in modes |

## Related

- [docs/design.md](docs/design.md)
- [nuget-only-policy](https://github.com/Novolis-Platform/novolis-governance/blob/main/docs/nuget-only-policy.md)
