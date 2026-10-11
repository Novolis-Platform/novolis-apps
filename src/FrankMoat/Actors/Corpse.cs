using System.Numerics;

namespace FrankMoat.Actors;

internal sealed class Corpse
{
    public required EnemyKind Kind { get; init; }

    public required Vector3 Position { get; init; }

    public float Rotation { get; init; }
}
