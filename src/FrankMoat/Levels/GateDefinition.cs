using System.Numerics;

namespace FrankMoat.Levels;

internal sealed class GateDefinition
{
    public required string Id { get; init; }

    public required Vector3 Position { get; init; }

    public required float Width { get; init; }

    public required string RequiredPermission { get; init; }

    public bool IsOpen { get; set; }
}
