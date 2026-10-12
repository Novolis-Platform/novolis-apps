using Android.Views;
using FrankMoat.Input;

namespace FrankMoat.Android;

public sealed class AndroidBluetoothGamepadReader : IGamepadReader
{
    private readonly object _gate = new();
    private GamepadButtons _buttons;
    private GamepadButtons _pressed;
    private float _moveX;
    private float _moveZ;
    private float _aimX;
    private float _aimZ;
    private float _leftTrigger;
    private float _rightTrigger;
    private float _lastRightTrigger;

    public WindowsGamepadSnapshot Read()
    {
        lock (_gate)
        {
            var rightTriggerWasPressed =
                _rightTrigger > 0.35f && _lastRightTrigger <= 0.35f;
            var snapshot = new WindowsGamepadSnapshot(
                _moveX,
                _moveZ,
                _aimX,
                _aimZ,
                _leftTrigger,
                _rightTrigger,
                _buttons,
                _pressed,
                rightTriggerWasPressed);
            _pressed = GamepadButtons.None;
            _lastRightTrigger = _rightTrigger;
            return snapshot;
        }
    }

    public bool HandleKeyDown(Keycode keyCode)
    {
        lock (_gate)
        {
            if (keyCode is Keycode.ButtonL2 or Keycode.ButtonR2)
            {
                if (keyCode == Keycode.ButtonL2)
                {
                    _leftTrigger = 1f;
                }
                else
                {
                    _rightTrigger = 1f;
                }

                return true;
            }

            if (!TryMapButton(keyCode, out var button))
            {
                return false;
            }

            if ((_buttons & button) == 0)
            {
                _pressed |= button;
            }

            _buttons |= button;
            return true;
        }
    }

    public bool HandleKeyUp(Keycode keyCode)
    {
        lock (_gate)
        {
            if (keyCode is Keycode.ButtonL2 or Keycode.ButtonR2)
            {
                if (keyCode == Keycode.ButtonL2)
                {
                    _leftTrigger = 0f;
                }
                else
                {
                    _rightTrigger = 0f;
                }

                return true;
            }

            if (!TryMapButton(keyCode, out var button))
            {
                return false;
            }

            _buttons &= ~button;
            return true;
        }
    }

    public bool HandleMotion(MotionEvent motion)
    {
        ArgumentNullException.ThrowIfNull(motion);
        if ((motion.Source & (InputSourceType.Gamepad | InputSourceType.Joystick)) == 0)
        {
            return false;
        }

        lock (_gate)
        {
            _moveX = ApplyDeadZone(motion.GetAxisValue(Axis.X));
            _moveZ = ApplyDeadZone(-motion.GetAxisValue(Axis.Y));
            _aimX = ApplyDeadZone(motion.GetAxisValue(Axis.Rx));
            _aimZ = ApplyDeadZone(-motion.GetAxisValue(Axis.Ry));
            _leftTrigger = TriggerValue(
                motion,
                Axis.Ltrigger,
                Axis.Brake);
            _rightTrigger = TriggerValue(
                motion,
                Axis.Rtrigger,
                Axis.Gas);
        }

        return true;
    }

    private static bool TryMapButton(Keycode keyCode, out GamepadButtons button)
    {
        button = keyCode switch
        {
            Keycode.DpadUp => GamepadButtons.DPadUp,
            Keycode.DpadDown => GamepadButtons.DPadDown,
            Keycode.DpadLeft => GamepadButtons.DPadLeft,
            Keycode.DpadRight => GamepadButtons.DPadRight,
            Keycode.ButtonStart => GamepadButtons.Start,
            Keycode.ButtonSelect => GamepadButtons.Back,
            Keycode.ButtonThumbl => GamepadButtons.LeftThumb,
            Keycode.ButtonThumbr => GamepadButtons.RightThumb,
            Keycode.ButtonL1 => GamepadButtons.LeftShoulder,
            Keycode.ButtonR1 => GamepadButtons.RightShoulder,
            Keycode.ButtonA => GamepadButtons.A,
            Keycode.ButtonB => GamepadButtons.B,
            Keycode.ButtonX => GamepadButtons.X,
            Keycode.ButtonY => GamepadButtons.Y,
            _ => GamepadButtons.None,
        };
        return button != GamepadButtons.None;
    }

    private static float TriggerValue(MotionEvent motion, Axis primary, Axis fallback)
    {
        var primaryValue = motion.GetAxisValue(primary);
        var fallbackValue = motion.GetAxisValue(fallback);
        return Math.Clamp(MathF.Max(primaryValue, fallbackValue), 0f, 1f);
    }

    private static float ApplyDeadZone(float value) =>
        MathF.Abs(value) < 0.16f
            ? 0f
            : Math.Clamp(value, -1f, 1f);
}
