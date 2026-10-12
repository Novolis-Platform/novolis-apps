using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using FrankMoat.Input;

namespace FrankMoat.Android;

[Application]
public class MainApplication : AvaloniaAndroidApplication<FrankMoat.App>
{
    public static AndroidBluetoothGamepadReader Gamepad { get; } = new();

    protected MainApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    public override void OnCreate()
    {
        FrankMoat.App.GamepadReaderFactory = static () => Gamepad;
        base.OnCreate();
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).LogToTrace();
}
