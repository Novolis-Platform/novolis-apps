# Frank Moat

Frank Moat is a Windows-first Avalonia alpha slice of the facility action game.
It currently covers the Level 0 lobby, the Level 69 founder preserve, and the
first connected Level 68 containment floor.

## Local run

```powershell
dotnet run --project d:\novolis\novolis-apps\src\FrankMoat\FrankMoat.csproj -p:NovolisUseProjectReferences=true
```

## Controls

- `WASD` / left stick: move
- Mouse / right stick: aim
- `C`: switch between mouse aim and arrow-key directional aim
- Arrow keys: aim while directional aim is active
- Left mouse / right trigger: fire
- `Shift` / left shoulder: run
- `R` / `X`: reload
- `E` / `A`: interact
- `F` / `B`: quick melee
- `1`–`5` / D-pad: choose weapon
- `Q` / `Y`: previous weapon
- `Tab` / Back: floor map
- `I` / both thumb sticks: inspect
- `Escape` / Start: pause
- `F5`: save checkpoint
- `F9`: restore checkpoint

The lab prototype remains under
`d:\novolis\novolis-lab\labs\gaming\FrankMoat`; this product owns its game
source and consumes platform libraries through `LibraryReference`.
