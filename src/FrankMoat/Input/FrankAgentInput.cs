namespace FrankMoat.Input;

internal readonly record struct FrankAgentInput(
    float MoveX,
    float MoveZ,
    bool HasMove,
    float AimX,
    float AimZ,
    bool HasAim,
    bool RunHeld,
    bool FireHeld,
    bool SecondaryHeld,
    bool ShootPressed,
    bool ReloadPressed,
    bool InteractPressed,
    bool MeleePressed,
    bool PreviousWeaponPressed,
    bool MapPressed,
    bool InspectPressed,
    bool PausePressed,
    bool SavePressed,
    bool LoadPressed,
    bool ControlModePressed,
    int WeaponSlot,
    DateTime ExpiresAtUtc)
{
    public string? SaveName { get; init; }

    public string? LoadName { get; init; }

    public static FrankAgentInput Empty => default;

    public bool IsActive =>
        ExpiresAtUtc > DateTime.UtcNow
        || ShootPressed
        || ReloadPressed
        || InteractPressed
        || MeleePressed
        || PreviousWeaponPressed
        || MapPressed
        || InspectPressed
        || PausePressed
        || SavePressed
        || LoadPressed
        || ControlModePressed
        || WeaponSlot != 0;

    public FrankAgentInput WithDuration(float seconds) =>
        this with
        {
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Clamp(seconds, 0.05f, 10f)),
        };

    public FrankAgentInput WithoutEdges() =>
        this with
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
            SaveName = null,
            LoadName = null,
        };
}
