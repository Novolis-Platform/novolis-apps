using Android.App;
using Android.Content.PM;
using Android.Views;
using Avalonia.Android;

namespace FrankMoat.Android;

[Activity(
    Label = "Frank Moat",
    Theme = "@style/MainTheme",
    MainLauncher = true,
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation
        | ConfigChanges.ScreenSize
        | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity
{
    public override bool OnKeyDown(Keycode keyCode, KeyEvent? e)
    {
        return MainApplication.Gamepad.HandleKeyDown(keyCode)
            || base.OnKeyDown(keyCode, e);
    }

    public override bool OnKeyUp(Keycode keyCode, KeyEvent? e)
    {
        return MainApplication.Gamepad.HandleKeyUp(keyCode)
            || base.OnKeyUp(keyCode, e);
    }

    public override bool OnGenericMotionEvent(MotionEvent? e)
    {
        if (e is not null && MainApplication.Gamepad.HandleMotion(e))
        {
            return true;
        }

        return base.OnGenericMotionEvent(e);
    }
}
