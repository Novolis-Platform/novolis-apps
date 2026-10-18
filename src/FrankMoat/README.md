# Frank Moat

Frank Moat is a Windows-first Avalonia alpha slice of the facility action game.
It currently covers the Level 0 lobby, the Level 69 founder preserve, and the
first connected Level 68 containment floor.

## Local run

```powershell
dotnet run --project d:\novolis\novolis-apps\src\FrankMoat\FrankMoat.Desktop\FrankMoat.Desktop.csproj -p:NovolisUseProjectReferences=true
```

To enable the local gameplay surface for agent control and snapshots:

```powershell
$env:NOVOLIS_FRANK_MOAT_AGENT = "1"
dotnet run --project d:\novolis\novolis-apps\src\FrankMoat\FrankMoat.Desktop\FrankMoat.Desktop.csproj -p:NovolisUseProjectReferences=true
```

The HTTP surface is available at `http://127.0.0.1:18885/agent/`; inspect
`/agent/actions` and `/agent/snapshot` before sending `/agent/command` requests.
The surface owns only agent input; keyboard, mouse, and controller input remain
available. Clear the environment variable to run without the surface.

Android builds use the Avalonia Android host and accept Bluetooth gamepads
through the native Android key and joystick motion events:

```powershell
dotnet build d:\novolis\novolis-apps\src\FrankMoat\FrankMoat.Android\FrankMoat.Android.csproj -p:NovolisUseProjectReferences=true
```

## Controls

- `WASD` / left stick: move in world mode
- `C` / right-stick click: switch between world movement and character-oriented movement
- Character mode: WASD / left stick moves relative to Frank's facing
- Mouse / right stick: aim in both movement modes
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

The pause menu also provides named checkpoint save/restore and `EXIT GAME`.
Named saves are stored beside the quick checkpoint under the local Frank Moat
save directory. The agent surface exposes `save` and `load` with an optional
`name` parameter, plus `exit`.

The lab prototype remains under
`d:\novolis\novolis-lab\labs\gaming\FrankMoat`; this product owns its game
source and consumes platform libraries through `LibraryReference`.
