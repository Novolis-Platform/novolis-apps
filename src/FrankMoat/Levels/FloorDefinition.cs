using System.Numerics;
using FrankMoat.Actors;

namespace FrankMoat.Levels;

internal sealed class FloorDefinition
{
    public required FloorId Id { get; init; }

    public required string Name { get; init; }

    public required Vector3 StartPosition { get; init; }

    public required IReadOnlyList<RoomDefinition> Rooms { get; init; }

    public required IReadOnlyList<RaisedWall> Walls { get; init; }

    public required IReadOnlyList<GateDefinition> Gates { get; init; }

    public required IReadOnlyList<EncounterDefinition> Encounters { get; init; }

    public required IReadOnlyList<SecretDefinition> Secrets { get; init; }

    public required IReadOnlyList<LightFixtureDefinition> Fixtures { get; init; }

    public required ObjectiveDefinition PrimaryObjective { get; init; }

    public string EntryPrompt { get; init; } = string.Empty;

    public string ExitPrompt { get; init; } = string.Empty;
}
