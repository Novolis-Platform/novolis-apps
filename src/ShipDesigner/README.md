# Ship Designer

Product host for decked freighter CAD — Open/Save `.cadjson`, airtight validation, Calypso seed import.

```powershell
dotnet run --project d:\novolis\novolis-apps\src\ShipDesigner\ShipDesigner.csproj -p:NovolisUseProjectReferences=true
dotnet run --project d:\novolis\novolis-apps\src\ShipDesigner\ShipDesigner.csproj -p:NovolisUseProjectReferences=true -- --smoke
```

Data root: `%LocalAppData%\Novolis\Ship Designer`

Calypso seed (after generate): `%LocalAppData%\Novolis\CalypsoCad\generated\calypso.cadjson` via **File → Import Calypso seed…**. Author exterior solids here; Calypso regenerate preserves `properties.exterior` / `ext-*` / `nacelle-*`.
