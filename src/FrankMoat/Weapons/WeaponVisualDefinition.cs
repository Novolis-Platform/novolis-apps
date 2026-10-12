using Novolis.Math.Geometry;

namespace FrankMoat.Weapons;

internal sealed record WeaponVisualDefinition(
    float Width,
    float Length,
    Rgba32 Body,
    Rgba32 Accent,
    bool Heavy);
