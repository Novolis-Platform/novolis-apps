using System.Numerics;

namespace FrankMoat.Levels;

internal sealed class EncounterDefinition
{
    public required string Id { get; init; }

    public required Vector3 TriggerPosition { get; init; }

    public required float TriggerRadius { get; init; }

    public required IReadOnlyList<EnemySpawnDefinition> Spawns { get; init; }

    public bool HasTriggered { get; set; }

    public bool IsComplete { get; set; }
}
