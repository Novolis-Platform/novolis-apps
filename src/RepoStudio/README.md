# Repo Studio

Hybrid CLI + Avalonia host for multi-repo Git over the Novolis workspace.

- Domain: `Novolis.IO.Git`
- Chrome: `Novolis.Avalonia.Git`

## Run

```powershell
dotnet run --project d:\novolis\novolis-apps\src\RepoStudio -p:NovolisUseProjectReferences=true
dotnet run --project d:\novolis\novolis-apps\src\RepoStudio -p:NovolisUseProjectReferences=true -- --mode spectre status --json
dotnet run --project d:\novolis\novolis-apps\src\RepoStudio -p:NovolisUseProjectReferences=true -- --mode spectre fetch --parallel 8
dotnet run --project d:\novolis\novolis-apps\src\RepoStudio -p:NovolisUseProjectReferences=true -- daemon --interval 600
```
