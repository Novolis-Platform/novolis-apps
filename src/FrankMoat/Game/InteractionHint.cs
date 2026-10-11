using System.Numerics;

namespace FrankMoat.Game;

internal sealed record InteractionHint(
    string Id,
    Vector3 Position,
    string Label,
    float InteractionRadius,
    bool IsAvailable,
    string LockReason)
{
    public float DistanceFrom(Vector3 origin) =>
        Vector3.Distance(
            new Vector3(origin.X, 0f, origin.Z),
            new Vector3(Position.X, 0f, Position.Z));

    public bool IsInRange(Vector3 origin) =>
        DistanceFrom(origin) <= InteractionRadius;

    public string PromptText =>
        IsAvailable
            ? $"[E] {Label}"
            : $"{Label} — {LockReason}";
}
