namespace FrankMoat.Levels;

internal sealed record RoomDefinition(
    string Id,
    string Label,
    float MinX,
    float MinZ,
    float MaxX,
    float MaxZ);
