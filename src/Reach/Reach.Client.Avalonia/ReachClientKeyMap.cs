using Avalonia.Input;

namespace Novolis.Avalonia.Reach;

internal static class ReachClientKeyMap
{
    internal static bool IsPrintable(Key key) =>
        key is >= Key.A and <= Key.Z
            or >= Key.D0 and <= Key.D9
            or >= Key.NumPad0 and <= Key.NumPad9
            or Key.Space
            or Key.OemPlus
            or Key.OemMinus
            or Key.OemComma
            or Key.OemPeriod
            or Key.OemQuestion
            or Key.OemOpenBrackets
            or Key.OemCloseBrackets
            or Key.OemPipe
            or Key.OemSemicolon
            or Key.OemQuotes
            or Key.OemTilde;

    internal static bool TryGetVirtualKey(Key key, out ushort virtualKey)
    {
        virtualKey = key switch
        {
            >= Key.A and <= Key.Z => (ushort)('A' + ((int)key - (int)Key.A)),
            >= Key.D0 and <= Key.D9 => (ushort)('0' + ((int)key - (int)Key.D0)),
            >= Key.NumPad0 and <= Key.NumPad9 =>
                (ushort)(0x60 + ((int)key - (int)Key.NumPad0)),
            Key.Back => 0x08,
            Key.Tab => 0x09,
            Key.Return => 0x0D,
            Key.Escape => 0x1B,
            Key.Space => 0x20,
            Key.Left => 0x25,
            Key.Up => 0x26,
            Key.Right => 0x27,
            Key.Down => 0x28,
            Key.Insert => 0x2D,
            Key.Delete => 0x2E,
            Key.Home => 0x24,
            Key.End => 0x23,
            Key.PageUp => 0x21,
            Key.PageDown => 0x22,
            Key.LeftShift or Key.RightShift => 0x10,
            Key.LeftCtrl or Key.RightCtrl => 0x11,
            Key.LeftAlt or Key.RightAlt => 0x12,
            Key.LWin => 0x5B,
            Key.RWin => 0x5C,
            Key.Apps => 0x5D,
            Key.CapsLock => 0x14,
            Key.NumLock => 0x90,
            Key.Scroll => 0x91,
            Key.PrintScreen => 0x2C,
            Key.Pause => 0x13,
            >= Key.F1 and <= Key.F12 => (ushort)(0x70 + ((int)key - (int)Key.F1)),
            Key.OemPlus or Key.Add => 0xBB,
            Key.OemMinus or Key.Subtract => 0xBD,
            Key.OemComma => 0xBC,
            Key.OemPeriod => 0xBE,
            Key.OemQuestion => 0xBF,
            Key.OemOpenBrackets => 0xDB,
            Key.OemCloseBrackets => 0xDD,
            Key.OemPipe => 0xDC,
            Key.OemSemicolon => 0xBA,
            Key.OemQuotes => 0xDE,
            Key.OemTilde => 0xC0,
            _ => 0,
        };
        return virtualKey != 0;
    }

    internal static string? GetPressedButton(
        PointerPointProperties properties,
        PointerType pointerType)
    {
        if (pointerType == PointerType.Touch)
            return "Left";
        if (properties.IsLeftButtonPressed)
            return "Left";
        if (properties.IsRightButtonPressed)
            return "Right";
        if (properties.IsMiddleButtonPressed)
            return "Middle";
        return null;
    }
}
