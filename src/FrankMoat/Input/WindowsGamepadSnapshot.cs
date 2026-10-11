namespace FrankMoat.Input;

internal readonly record struct WindowsGamepadSnapshot(
    float MoveX,
    float MoveZ,
    float AimX,
    float AimZ,
    float LeftTrigger,
    float RightTrigger,
    ushort Buttons,
    ushort Pressed,
    bool RightTriggerWasPressed);
