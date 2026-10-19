using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Novolis.Avalonia.Mobile.Android;

namespace SketchStudio.Android;

/// <summary>Android application host for Sketch Studio.</summary>
[global::Android.App.Application]
public class MainApplication : AvaloniaAndroidApplication<App>
{
    /// <summary>JNI constructor.</summary>
    protected MainApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    /// <inheritdoc />
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).LogToTrace();
}
