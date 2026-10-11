using System.Numerics;

namespace FrankMoat.Levels;

internal sealed class SecretDefinition
{
    public required string Id { get; init; }

    public required Vector3 Position { get; init; }

    public required string RewardText { get; init; }

    public bool Discovered { get; set; }
}
