using System.Numerics;

namespace FrankMoat.Levels;

internal sealed record LightFixtureDefinition(
    Vector3 Position,
    RgbaLightColor Color,
    float Radius,
    float Intensity,
    bool EmergencyOnly = false);
