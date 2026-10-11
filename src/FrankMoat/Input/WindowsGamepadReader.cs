using System.Runtime.InteropServices;

namespace FrankMoat.Input;

internal sealed class WindowsGamepadReader
{
    private const ushort DPadUp = 0x0001;
    private const ushort DPadDown = 0x0002;
    private const ushort DPadLeft = 0x0004;
    private const ushort DPadRight = 0x0008;
    private const ushort Start = 0x0010;
    private const ushort Back = 0x0020;
    private const ushort LeftThumb = 0x0040;
    private const ushort RightThumb = 0x0080;
    private const ushort LeftShoulder = 0x0100;
    private const ushort RightShoulder = 0x0200;
    private const ushort A = 0x1000;
    private const ushort B = 0x2000;
    private const ushort X = 0x4000;
    private const ushort Y = 0x8000;

    private ushort _previousButtons;
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
                _previousButtons = 0;
                return default;
            }

            var buttons = state.Gamepad.Buttons;
            var pressed = (ushort)(buttons & ~_previousButtons);
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

    public static bool IsDown(WindowsGamepadSnapshot snapshot, ushort button) =>
        (snapshot.Buttons & button) != 0;

    public static bool WasPressed(WindowsGamepadSnapshot snapshot, ushort button) =>
        (snapshot.Pressed & button) != 0;

    public static bool IsDPadUp(WindowsGamepadSnapshot snapshot) => IsDown(snapshot, DPadUp);

    public static bool IsDPadDown(WindowsGamepadSnapshot snapshot) => IsDown(snapshot, DPadDown);

    public static bool IsDPadLeft(WindowsGamepadSnapshot snapshot) => IsDown(snapshot, DPadLeft);

    public static bool IsDPadRight(WindowsGamepadSnapshot snapshot) => IsDown(snapshot, DPadRight);

    public static bool IsStartDown(WindowsGamepadSnapshot snapshot) => WasPressed(snapshot, Start);

    public static bool IsBackDown(WindowsGamepadSnapshot snapshot) => WasPressed(snapshot, Back);

    public static bool IsASelected(WindowsGamepadSnapshot snapshot) => WasPressed(snapshot, A);

    public static bool IsBSelected(WindowsGamepadSnapshot snapshot) => WasPressed(snapshot, B);

    public static bool IsXSelected(WindowsGamepadSnapshot snapshot) => WasPressed(snapshot, X);

    public static bool IsYSelected(WindowsGamepadSnapshot snapshot) => WasPressed(snapshot, Y);

    public static bool IsLeftShoulderDown(WindowsGamepadSnapshot snapshot) =>
        IsDown(snapshot, LeftShoulder);

    public static bool IsRightShoulderDown(WindowsGamepadSnapshot snapshot) =>
        IsDown(snapshot, RightShoulder);

    public static bool IsLeftThumbDown(WindowsGamepadSnapshot snapshot) =>
        IsDown(snapshot, LeftThumb);

    public static bool IsRightThumbDown(WindowsGamepadSnapshot snapshot) =>
        IsDown(snapshot, RightThumb);

    public static bool IsLeftThumbPressed(WindowsGamepadSnapshot snapshot) =>
        WasPressed(snapshot, LeftThumb);

    public static bool IsRightThumbPressed(WindowsGamepadSnapshot snapshot) =>
        WasPressed(snapshot, RightThumb);

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
