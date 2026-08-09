# Ship Designer

Object-first ship architecture host — Open/Save `.shipjson` (`novolis.ship`), PLAN / MODEL / PRESENT workspaces, structure-first create, Calypso `.cadjson` import.

```powershell
dotnet run --project d:\novolis\novolis-apps\src\ShipDesigner\ShipDesigner.csproj -p:NovolisUseProjectReferences=true
dotnet run --project d:\novolis\novolis-apps\src\ShipDesigner\ShipDesigner.csproj -p:NovolisUseProjectReferences=true -- --smoke
```

Data root: `%LocalAppData%\Novolis\Ship Designer`

Calypso seed (after generate): `%LocalAppData%\Novolis\CalypsoCad\generated\calypso.cadjson` via **File → Import Calypso seed…** (projects into `ShipDesign`). Flat `.cadjson` remains an export/import bridge; authoring SoT is `.shipjson`.
