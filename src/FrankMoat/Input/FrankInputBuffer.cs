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

    public ControlMode ControlMode { get; private set; } = ControlMode.IndependentMovement;

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
        ControlMode = ControlMode == ControlMode.IndependentMovement
            ? ControlMode.CharacterOriented
            : ControlMode.IndependentMovement;

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
        Vector3 playerFacing,
        WindowsGamepadSnapshot gamepad,
        FrankAgentInput agent = default)
    {
        var rawMoveX = agent.HasMove
            ? agent.MoveX
            : Axis(IsDown(Key.A), IsDown(Key.D)) + gamepad.MoveX;
        var rawMoveZ = agent.HasMove
            ? agent.MoveZ
            : Axis(IsDown(Key.S), IsDown(Key.W)) + gamepad.MoveZ;
        var moveX = rawMoveX;
        var moveZ = rawMoveZ;
        var forward = playerFacing.LengthSquaredXz() > 0.01f
            ? playerFacing.NormalizeXz()
            : Vector3.UnitZ;
        if (ControlMode == ControlMode.CharacterOriented)
        {
            var right = new Vector3(forward.Z, 0f, -forward.X);
            var oriented = right * rawMoveX + forward * rawMoveZ;
            moveX = oriented.X;
            moveZ = oriented.Z;
        }

        var moveLength = MathF.Sqrt(moveX * moveX + moveZ * moveZ);
        if (moveLength > 1f)
        {
            moveX /= moveLength;
            moveZ /= moveLength;
        }

        var aim = scene.Camera.ScreenToWorld(mousePixels.X, mousePixels.Y) - playerPosition;
        if (agent.HasAim)
        {
            aim = new Vector3(agent.AimX, 0f, agent.AimZ);
        }
        else if (gamepad.AimX != 0f || gamepad.AimZ != 0f)
        {
            aim = new Vector3(gamepad.AimX, 0f, gamepad.AimZ);
        }

        if (aim.LengthSquaredXz() < 0.01f)
        {
            aim = forward;
        }
        else
        {
            aim = aim.NormalizeXz();
        }

        var slot = agent.WeaponSlot;
        if (slot == 0)
        {
            slot = SlotFromKeyboard();
        }

        if (slot == 0)
        {
            slot = SlotFromGamepad(gamepad);
        }

        var shootPressed = agent.ShootPressed
            || _edges.Contains(FrankAction.Shoot)
            || gamepad.RightTrigger > 0.35f && gamepad.RightTriggerWasPressed;
        var pausePressed = agent.PausePressed
            || _edges.Contains(FrankAction.Pause)
            || WindowsGamepadReader.IsStartDown(gamepad);
        var inspectPressed = _edges.Contains(FrankAction.Inspect)
            || agent.InspectPressed
            || WindowsGamepadReader.IsLeftThumbPressed(gamepad)
                && WindowsGamepadReader.IsRightThumbDown(gamepad)
            || WindowsGamepadReader.IsRightThumbPressed(gamepad)
                && WindowsGamepadReader.IsLeftThumbDown(gamepad);
        var controlModePressed = _edges.Contains(FrankAction.ToggleControlMode)
            || agent.ControlModePressed
            || WindowsGamepadReader.IsRightThumbPressed(gamepad) && !inspectPressed;

        return new FrankInputFrame(
            moveX,
            moveZ,
            aim,
            agent.RunHeld || IsDown(Key.LeftShift) || IsDown(Key.RightShift)
                || WindowsGamepadReader.IsLeftShoulderDown(gamepad),
            agent.FireHeld || _leftMouseDown || gamepad.RightTrigger > 0.35f,
            shootPressed,
            agent.SecondaryHeld || _rightMouseDown || gamepad.LeftTrigger > 0.35f,
            agent.ReloadPressed
                || _edges.Contains(FrankAction.Reload)
                || WindowsGamepadReader.IsXSelected(gamepad),
            agent.InteractPressed
                || _edges.Contains(FrankAction.Interact)
                || WindowsGamepadReader.IsASelected(gamepad),
            agent.MeleePressed
                || _edges.Contains(FrankAction.Melee)
                || WindowsGamepadReader.IsBSelected(gamepad),
            agent.PreviousWeaponPressed
                || _edges.Contains(FrankAction.PreviousWeapon)
                || WindowsGamepadReader.IsYSelected(gamepad),
            agent.MapPressed
                || _edges.Contains(FrankAction.Map)
                || WindowsGamepadReader.IsBackDown(gamepad),
            inspectPressed,
            pausePressed,
            agent.SavePressed || _edges.Contains(FrankAction.Save),
            agent.LoadPressed || _edges.Contains(FrankAction.Load),
            slot,
            ZoomTicks())
        {
            ControlModePressed = controlModePressed,
            AgentFireHeld = agent.FireHeld,
            SaveName = agent.SaveName,
            LoadName = agent.LoadName,
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
