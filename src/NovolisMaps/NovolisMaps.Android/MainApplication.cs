using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Avalonia.Mobile.Android;

namespace NovolisMaps.Android;

/// <summary>Android application host for Novolis Maps.</summary>
[global::Android.App.Application]
public class MainApplication : AvaloniaAndroidApplication<global::NovolisMaps.App>
{
    /// <summary>JNI constructor.</summary>
    protected MainApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    /// <inheritdoc />
    public override void OnCreate()
    {
        var services = new ServiceCollection();
        services.AddNovolisMobileAndroid("NovolisMaps");
        services.AddNovolisMaps();
        global::NovolisMaps.App.Services = services.BuildServiceProvider();
        base.OnCreate();
    }

    /// <inheritdoc />
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).LogToTrace();
}
