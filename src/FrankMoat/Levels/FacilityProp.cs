using System.Numerics;

namespace FrankMoat.Levels;

internal sealed record FacilityProp(
    string Id,
    PropKind Kind,
    Vector3 Position,
    Vector2 Size,
    float Elevation = 0.05f,
    float Rotation = 0f);
