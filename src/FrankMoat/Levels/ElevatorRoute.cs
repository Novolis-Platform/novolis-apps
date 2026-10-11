namespace FrankMoat.Levels;

internal sealed record ElevatorRoute(
    FloorId From,
    FloorId To,
    string Name,
    string RequiredFlag,
    bool IsExpress = false);
