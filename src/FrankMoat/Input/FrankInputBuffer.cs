using System.Numerics;
using Avalonia.Input;
using Novolis.Math.Geometry;
using Novolis.Rendering.Planar;
using PresentationMouseButton = Novolis.Rendering.Presentation.MouseButton;

namespace FrankMoat.Input;

internal sealed class FrankInputBuffer
{
    private readonly HashSet<Key> _keysDown = [];
    private readonly HashSet<FrankAction> _edges = [];
    private bool _leftMouseDown;
    private bool _rightMouseDown;
    private Vector3 _lastAim = Vector3.UnitZ;

    public ControlMode ControlMode { get; private set; } = ControlMode.MouseAim;

    public void KeyDown(Key key)
    {
        if (!_keysDown.Add(key))
        {
            return;
        }

        if (TryMapAction(key, out var action))
        {
            _edges.Add(action);
        }
    }

    public void KeyUp(Key key) => _keysDown.Remove(key);

    public void ToggleControlMode() =>
        ControlMode = ControlMode == ControlMode.MouseAim
            ? ControlMode.DirectionalAim
            : ControlMode.MouseAim;

    public void PointerPressed(PresentationMouseButton button)
    {
        if (button == PresentationMouseButton.Left)
        {
            _leftMouseDown = true;
        }
        else if (button == PresentationMouseButton.Right)
        {
            _rightMouseDown = true;
        }
    }

    public void PointerReleased(PresentationMouseButton button)
    {
        if (button == PresentationMouseButton.Left)
        {
            _leftMouseDown = false;
        }
        else if (button == PresentationMouseButton.Right)
        {
            _rightMouseDown = false;
        }
    }

    public FrankInputFrame BuildFrame(
        PlanarScene scene,
        Vector2 mousePixels,
        int pixelWidth,
        int pixelHeight,
        Vector3 playerPosition,
        WindowsGamepadSnapshot gamepad)
    {
        var moveX = Axis(IsDown(Key.A), IsDown(Key.D)) + gamepad.MoveX;
        var moveZ = Axis(IsDown(Key.S), IsDown(Key.W)) + gamepad.MoveZ;
        var moveLength = MathF.Sqrt(moveX * moveX + moveZ * moveZ);
        if (moveLength > 1f)
        {
            moveX /= moveLength;
            moveZ /= moveLength;
        }

        var aim = ControlMode == ControlMode.DirectionalAim
            ? DirectionalAim()
            : scene.Camera.ScreenToWorld(mousePixels.X, mousePixels.Y) - playerPosition;
        if (gamepad.AimX != 0f || gamepad.AimZ != 0f)
        {
            aim = new Vector3(gamepad.AimX, 0f, gamepad.AimZ);
        }

        if (aim.LengthSquaredXz() < 0.01f)
        {
            aim = _lastAim;
        }
        else
        {
            aim = aim.NormalizeXz();
            _lastAim = aim;
        }

        var slot = SlotFromKeyboard();
        if (slot == 0)
        {
            slot = SlotFromGamepad(gamepad);
        }

        var shootPressed = _edges.Contains(FrankAction.Shoot)
            || gamepad.RightTrigger > 0.35f && gamepad.RightTriggerWasPressed;
        var pausePressed = _edges.Contains(FrankAction.Pause)
            || WindowsGamepadReader.IsStartDown(gamepad);

        return new FrankInputFrame(
            moveX,
            moveZ,
            aim,
            IsDown(Key.LeftShift) || IsDown(Key.RightShift)
                || WindowsGamepadReader.IsLeftShoulderDown(gamepad),
            _leftMouseDown || gamepad.RightTrigger > 0.35f,
            shootPressed,
            _rightMouseDown || gamepad.LeftTrigger > 0.35f,
            _edges.Contains(FrankAction.Reload) || WindowsGamepadReader.IsXSelected(gamepad),
            _edges.Contains(FrankAction.Interact) || WindowsGamepadReader.IsASelected(gamepad),
            _edges.Contains(FrankAction.Melee) || WindowsGamepadReader.IsBSelected(gamepad),
            _edges.Contains(FrankAction.PreviousWeapon) || WindowsGamepadReader.IsYSelected(gamepad),
            _edges.Contains(FrankAction.Map) || WindowsGamepadReader.IsBackDown(gamepad),
            _edges.Contains(FrankAction.Inspect)
                || WindowsGamepadReader.IsLeftThumbPressed(gamepad)
                && WindowsGamepadReader.IsRightThumbDown(gamepad)
                || WindowsGamepadReader.IsRightThumbPressed(gamepad)
                && WindowsGamepadReader.IsLeftThumbDown(gamepad),
            pausePressed,
            _edges.Contains(FrankAction.Save),
            _edges.Contains(FrankAction.Load),
            slot,
            ZoomTicks())
        {
            ControlModePressed = _edges.Contains(FrankAction.ToggleControlMode),
        };
    }

    public void ConsumeEdges() => _edges.Clear();

    private bool IsDown(Key key) => _keysDown.Contains(key);

    private int ZoomTicks() =>
        _edges.Contains(FrankAction.ZoomIn) ? 1 :
        _edges.Contains(FrankAction.ZoomOut) ? -1 :
        0;

    private int SlotFromKeyboard()
    {
        if (IsDown(Key.D1))
        {
            return 1;
        }

        if (IsDown(Key.D2))
        {
            return 2;
        }

        if (IsDown(Key.D3))
        {
            return 3;
        }

        if (IsDown(Key.D4))
        {
            return 4;
        }

        return IsDown(Key.D5) ? 5 : 0;
    }

    private static int SlotFromGamepad(WindowsGamepadSnapshot gamepad)
    {
        if (WindowsGamepadReader.IsDPadUp(gamepad))
        {
            return 1;
        }

        if (WindowsGamepadReader.IsDPadRight(gamepad))
        {
            return 2;
        }

        if (WindowsGamepadReader.IsDPadDown(gamepad))
        {
            return 3;
        }

        return WindowsGamepadReader.IsDPadLeft(gamepad) ? 4 : 0;
    }

    private static float Axis(bool negative, bool positive) =>
        (positive ? 1f : 0f) - (negative ? 1f : 0f);

    private Vector3 DirectionalAim() =>
        new(
            Axis(IsDown(Key.Left), IsDown(Key.Right)),
            0f,
            Axis(IsDown(Key.Down), IsDown(Key.Up)));

    private static bool TryMapAction(Key key, out FrankAction action)
    {
        action = key switch
        {
            Key.R => FrankAction.Reload,
            Key.E => FrankAction.Interact,
            Key.Enter => FrankAction.Interact,
            Key.F => FrankAction.Melee,
            Key.Q => FrankAction.PreviousWeapon,
            Key.Tab => FrankAction.Map,
            Key.I => FrankAction.Inspect,
            Key.Escape => FrankAction.Pause,
            Key.F5 => FrankAction.Save,
            Key.F9 => FrankAction.Load,
            Key.Z => FrankAction.ZoomOut,
            Key.X => FrankAction.ZoomIn,
            Key.C => FrankAction.ToggleControlMode,
            Key.D1 => FrankAction.SlotOne,
            Key.D2 => FrankAction.SlotTwo,
            Key.D3 => FrankAction.SlotThree,
            Key.D4 => FrankAction.SlotFour,
            Key.D5 => FrankAction.SlotFive,
            _ => default,
        };

        return key is Key.R
            or Key.E
            or Key.Enter
            or Key.F
            or Key.Q
            or Key.Tab
            or Key.I
            or Key.Escape
            or Key.F5
            or Key.F9
            or Key.Z
            or Key.X
            or Key.C
            or Key.D1
            or Key.D2
            or Key.D3
            or Key.D4
            or Key.D5;
    }
}
