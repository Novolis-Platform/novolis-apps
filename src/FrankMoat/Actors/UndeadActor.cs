using System.Numerics;

namespace FrankMoat.Actors;

internal sealed class UndeadActor
{
    public required EnemyKind Kind { get; init; }

    public Vector3 Position { get; set; }

    public Vector3 Facing { get; set; } = new(0f, 0f, -1f);

    public int Health { get; set; }

    public float Radius { get; init; }

    public float Speed { get; init; }

    public float AttackTimer { get; set; }

    public float PerceptionTimer { get; set; }

    public bool Alerted { get; set; }

    public float HitFlash { get; set; }

    public bool IsRanged => Kind == EnemyKind.ContaminatedSecurity;

    public static UndeadActor Create(EnemyKind kind, Vector3 position) => kind switch
    {
        EnemyKind.Runner => new UndeadActor
        {
            Kind = kind,
            Position = position,
            Health = 36,
            Radius = 0.32f,
            Speed = 4.6f,
        },
        EnemyKind.ContaminatedSecurity => new UndeadActor
        {
            Kind = kind,
            Position = position,
            Health = 84,
            Radius = 0.4f,
            Speed = 2.7f,
        },
        _ => new UndeadActor
        {
            Kind = kind,
            Position = position,
            Health = 48,
            Radius = 0.36f,
            Speed = 2.4f,
        },
    };
}
