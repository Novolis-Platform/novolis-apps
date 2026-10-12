namespace FrankMoat.Input;

public readonly record struct WindowsGamepadSnapshot(
    float MoveX,
    float MoveZ,
    float AimX,
    float AimZ,
    float LeftTrigger,
    float RightTrigger,
    GamepadButtons Buttons,
    GamepadButtons Pressed,
    bool RightTriggerWasPressed);
