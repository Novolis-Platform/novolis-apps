using System.Numerics;

namespace FrankMoat.Input;

internal readonly record struct FrankInputFrame(
    float MoveX,
    float MoveZ,
    Vector3 AimWorld,
    bool RunHeld,
    bool ShootHeld,
    bool ShootPressed,
    bool SecondaryHeld,
    bool ReloadPressed,
    bool InteractPressed,
    bool MeleePressed,
    bool PreviousWeaponPressed,
    bool MapPressed,
    bool InspectPressed,
    bool PausePressed,
    bool SavePressed,
    bool LoadPressed,
    int WeaponSlot,
    int ZoomTicks)
{
    public bool ControlModePressed { get; init; }

    public FrankInputFrame WithoutEdges() => this with
    {
        ShootPressed = false,
        ReloadPressed = false,
        InteractPressed = false,
        MeleePressed = false,
        PreviousWeaponPressed = false,
        MapPressed = false,
        InspectPressed = false,
        PausePressed = false,
        SavePressed = false,
        LoadPressed = false,
        ControlModePressed = false,
        WeaponSlot = 0,
        ZoomTicks = 0,
    };
}
