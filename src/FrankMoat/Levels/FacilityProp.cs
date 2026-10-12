using System.Numerics;
using Novolis.Math.Topology;

namespace FrankMoat.Levels;

internal sealed record FacilityProp(
    string Id,
    PropKind Kind,
    Vector3 Position,
    Vector2 Size,
    float Elevation = 0.05f,
    float Rotation = 0f,
    float Height = 0f,
    bool BlocksMovement = true)
{
    public Polygon GetFootprint()
    {
        var halfX = Size.X * 0.5f;
        var halfZ = Size.Y * 0.5f;
        var cosine = MathF.Cos(Rotation);
        var sine = MathF.Sin(Rotation);
        var corners = new[]
        {
            new Vector3(-halfX, 0f, -halfZ),
            new Vector3(halfX, 0f, -halfZ),
            new Vector3(halfX, 0f, halfZ),
            new Vector3(-halfX, 0f, halfZ),
        };
        for (var i = 0; i < corners.Length; i++)
        {
            var corner = corners[i];
            corners[i] = new Vector3(
                Position.X + corner.X * cosine - corner.Z * sine,
                0f,
                Position.Z + corner.X * sine + corner.Z * cosine);
        }

        return new Polygon(corners);
    }
}
