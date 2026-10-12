using System.Runtime.InteropServices;

namespace FrankMoat.Input;

internal sealed class WindowsGamepadReader : IGamepadReader
{
    private GamepadButtons _previousButtons;
    private float _previousRightTrigger;
    private bool _available = true;

    public WindowsGamepadSnapshot Read()
    {
        if (!_available || !OperatingSystem.IsWindows())
        {
            return default;
        }

        try
        {
            if (XInputGetState(0, out var state) != 0)
            {
                _previousButtons = GamepadButtons.None;
                _previousRightTrigger = 0f;
                return default;
            }

            var buttons = (GamepadButtons)state.Gamepad.Buttons;
            var pressed = buttons & ~_previousButtons;
            var rightTrigger = state.Gamepad.RightTrigger / 255f;
            var rightTriggerWasPressed = rightTrigger > 0.35f && _previousRightTrigger <= 0.35f;
            _previousButtons = buttons;
            _previousRightTrigger = rightTrigger;
            return new WindowsGamepadSnapshot(
                NormalizeAxis(state.Gamepad.LeftThumbX),
                NormalizeAxis(state.Gamepad.LeftThumbY),
                NormalizeAxis(state.Gamepad.RightThumbX),
                NormalizeAxis(state.Gamepad.RightThumbY),
                state.Gamepad.LeftTrigger / 255f,
                rightTrigger,
                buttons,
                pressed,
                rightTriggerWasPressed);
        }
        catch (DllNotFoundException)
        {
            _available = false;
            return default;
        }
        catch (EntryPointNotFoundException)
        {
            _available = false;
            return default;
        }
    }

    public static bool IsDown(WindowsGamepadSnapshot snapshot, GamepadButtons button) =>
        (snapshot.Buttons & button) != 0;

    public static bool WasPressed(WindowsGamepadSnapshot snapshot, GamepadButtons button) =>
        (snapshot.Pressed & button) != 0;

    public static bool IsDPadUp(WindowsGamepadSnapshot snapshot) =>
        IsDown(snapshot, GamepadButtons.DPadUp);

    public static bool IsDPadDown(WindowsGamepadSnapshot snapshot) =>
        IsDown(snapshot, GamepadButtons.DPadDown);

    public static bool IsDPadLeft(WindowsGamepadSnapshot snapshot) =>
        IsDown(snapshot, GamepadButtons.DPadLeft);

    public static bool IsDPadRight(WindowsGamepadSnapshot snapshot) =>
        IsDown(snapshot, GamepadButtons.DPadRight);

    public static bool IsStartDown(WindowsGamepadSnapshot snapshot) =>
        WasPressed(snapshot, GamepadButtons.Start);

    public static bool IsBackDown(WindowsGamepadSnapshot snapshot) =>
        WasPressed(snapshot, GamepadButtons.Back);

    public static bool IsASelected(WindowsGamepadSnapshot snapshot) =>
        WasPressed(snapshot, GamepadButtons.A);

    public static bool IsBSelected(WindowsGamepadSnapshot snapshot) =>
        WasPressed(snapshot, GamepadButtons.B);

    public static bool IsXSelected(WindowsGamepadSnapshot snapshot) =>
        WasPressed(snapshot, GamepadButtons.X);

    public static bool IsYSelected(WindowsGamepadSnapshot snapshot) =>
        WasPressed(snapshot, GamepadButtons.Y);

    public static bool IsLeftShoulderDown(WindowsGamepadSnapshot snapshot) =>
        IsDown(snapshot, GamepadButtons.LeftShoulder);

    public static bool IsRightShoulderDown(WindowsGamepadSnapshot snapshot) =>
        IsDown(snapshot, GamepadButtons.RightShoulder);

    public static bool IsLeftThumbDown(WindowsGamepadSnapshot snapshot) =>
        IsDown(snapshot, GamepadButtons.LeftThumb);

    public static bool IsRightThumbDown(WindowsGamepadSnapshot snapshot) =>
        IsDown(snapshot, GamepadButtons.RightThumb);

    public static bool IsLeftThumbPressed(WindowsGamepadSnapshot snapshot) =>
        WasPressed(snapshot, GamepadButtons.LeftThumb);

    public static bool IsRightThumbPressed(WindowsGamepadSnapshot snapshot) =>
        WasPressed(snapshot, GamepadButtons.RightThumb);

    private static float NormalizeAxis(short value)
    {
        var normalized = value < 0 ? value / 32768f : value / 32767f;
        return MathF.Abs(normalized) < 0.16f ? 0f : Math.Clamp(normalized, -1f, 1f);
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState(uint userIndex, out XInputState state);

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short LeftThumbX;
        public short LeftThumbY;
        public short RightThumbX;
        public short RightThumbY;
    }
}
