using System.Numerics;

namespace FrankMoat.Actors;

internal sealed class FrankActor
{
    public const float Radius = 0.38f;
    public const float WalkSpeed = 6.8f;
    public const float RunSpeed = 9.2f;

    public Vector3 Position { get; set; } = new(6f, 0f, 6f);

    public Vector3 Facing { get; set; } = new(0f, 0f, 1f);

    public int Health { get; set; } = 100;

    public int Armor { get; set; } = 25;

    public float IFrames { get; set; }
}
