The framework is essentially a **C# authoring/compiler system for interactive fiction**, not a browser game engine.

### Core idea

Authors define a game using strongly typed C# and/or data files. Everything is normalized into one canonical game model, validated, then compiled into a **single self-contained HTML file** containing:

- HTML/CSS UI
- tiny JavaScript runtime
- serialized game data
- embedded images/audio/assets
- save/load support

No server or .NET runtime is needed after compilation.

### Core domain

Keep the semantic model very small:

```text
Game
Scene
Presentation
Interaction
Condition
Effect
State
Asset
```

A game is fundamentally:

> **An immutable graph of presentations and conditional interactions that transform serializable game state.**

That model supports both visual novels and point-and-click adventures.

### Unified interaction model

A hotspot and a dialogue choice are the same concept:

```text
When condition is true
→ present interaction
→ player selects it
→ execute effects
```

Effects include things such as:

```text
GoTo(scene)
SetFlag(...)
GiveItem(...)
RemoveItem(...)
Say(...)
Narrate(...)
PlaySound(...)
ChangeVariable(...)
```

Conditions are declarative:

```text
HasItem(...)
Flag(...)
Equals(...)
All(...)
Any(...)
Not(...)
```

Avoid arbitrary C# delegates because the compiler must serialize the behavior into the browser runtime.

### State

Definitions remain immutable. Runtime state contains things such as:

```text
CurrentScene
Flags
Variables
Inventory
CompletedInteractions
```

Effects conceptually operate as:

```text
GameState → GameState
```

This makes the actual game logic deterministic and trivially testable without a browser.

### C# and data are peers

C# is an authoring surface, not the storage format.

These could all eventually produce the exact same canonical representation:

```text
Game.cs
game.json
game.yaml
graphical editor
LLM-generated content
```

That keeps the engine independent of how content was created.

### Compiler

Pipeline:

```text
Authoring
   ↓
Canonical IR
   ↓
Validation
   ↓
Graph analysis
   ↓
Asset resolution/optimization
   ↓
HTML generation
```

The compiler should catch:

- missing references
- unreachable scenes
- impossible inventory requirements
- dead choices
- potential soft-locks
- missing assets
- invalid state transitions

This static analysis is one of the strongest reasons to use C# and an explicit graph model.

### Browser runtime

Keep it deliberately tiny.

It should interpret perhaps a few dozen standardized operations instead of running arbitrary game code.

Prefer normal HTML, CSS and SVG for:

- dialogue
- hotspots
- UI
- inventory
- scene composition
- accessibility

Canvas can be optional for graphical effects.

### Strong typing

Use typed IDs:

```csharp
public readonly record struct SceneId(string Value);
public readonly record struct ItemId(string Value);
public readonly record struct CharacterId(string Value);
public readonly record struct FlagId(string Value);
```

Source generators can turn assets and declared objects into compile-time-safe references:

```csharp
Assets.Kitchen
Scenes.Hallway
Items.BrassKey
```

So renames work naturally in Rider and typos become compiler errors.

### Testing

Most tests require no browser:

```csharp
var result = game.Play(
    Interactions.EnterKitchen,
    Interactions.OpenCupboard,
    Interactions.TakeKey,
    Interactions.UnlockDoor);

result.Scene.Should().Be(Scenes.Hallway);
```

Browser tests only need to verify that visual input maps correctly onto the deterministic game model.

### Scope

The same framework should naturally support:

- point-and-click adventures
- visual novels
- choose-your-own-adventure games
- escape rooms
- detective games
- dating sims
- interactive training
- dialogue-heavy RPG sections

without becoming a general-purpose game engine.

The key design decision is therefore: **standardize the game semantics first, then make C#, JSON, editors and HTML merely adapters around that semantic model.**