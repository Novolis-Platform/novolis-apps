# Novolis Maps

Novolis Maps is a small local-first map explorer composed from the shared
Novolis map stack. It provides:

- Kartverket topographic imagery with Norwegian address search.
- OpenStreetMap street imagery with worldwide Nominatim search.
- Saved pins stored locally in `pins.json`.
- Coordinate selection and clipboard copy.
- Route and area measurement without an account.

Map imagery and address search are the only network-backed features. Saved
pins stay on the current device.

## Projects

| Project | Role |
| --- | --- |
| `NovolisMaps` | Shared Avalonia view, map providers, and pin storage |
| `NovolisMaps.Desktop` | Windows host |
| `NovolisMaps.Android` | Android host |
| `NovolisMaps.Unit` | Pin persistence and provider composition tests |

## Run

```powershell
dotnet run --project src/NovolisMaps/NovolisMaps.Desktop/NovolisMaps.Desktop.csproj
```

Build the Android package with the Android workload:

```powershell
dotnet build src/NovolisMaps/NovolisMaps.Android/NovolisMaps.Android.csproj -c Release
```

## Data and providers

Pins are written atomically under the product data root. Tiles use the shared
bounded cache from `Novolis.IO.Maps`; changing the provider uses a separate
cache partition. The app shows each provider's required attribution while its
imagery is visible.
