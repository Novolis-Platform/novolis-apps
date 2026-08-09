# Ship Designer

Object-first spacecraft architecture host — Open/Save `.shipjson` (`novolis.ship` v2), PLAN / MODEL / ANALYZE workspaces, structure-first create with environment + load cases, continuous GREEN/YELLOW/RED analysis, Calypso `.cadjson` import.

```powershell
dotnet run --project d:\novolis\novolis-apps\src\ShipDesigner\ShipDesigner.csproj -p:NovolisUseProjectReferences=true
dotnet run --project d:\novolis\novolis-apps\src\ShipDesigner\ShipDesigner.csproj -p:NovolisUseProjectReferences=true -- --smoke
```

Data root: `%LocalAppData%\Novolis\Ship Designer`

Calypso seed (after generate): `%LocalAppData%\Novolis\CalypsoCad\generated\calypso.cadjson` via **File → Import Calypso seed…** (projects into `ShipDesign`). Flat `.cadjson` remains an export/import bridge; authoring SoT is `.shipjson`.
