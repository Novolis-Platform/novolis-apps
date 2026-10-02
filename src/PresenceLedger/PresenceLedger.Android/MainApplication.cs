using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Avalonia.Mobile.Android;
using PresenceLedger.App;
using PresenceLedgerApp = PresenceLedger.App.App;

namespace PresenceLedger.Android;

/// <summary>Android application host that initializes DI before Avalonia.</summary>
[Application]
public class MainApplication : AvaloniaAndroidApplication<PresenceLedgerApp>
{
    /// <summary>JNI constructor.</summary>
    protected MainApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    /// <inheritdoc />
    public override void OnCreate()
    {
        // Use the minimal container on Android so process startup does not
        // synchronously load the generic host configuration pipeline.
        var services = new ServiceCollection();
        services.AddNovolisMobileAndroid("PresenceLedger");
        services.AddPresenceLedger();
        services.AddSingleton<ILedgerFilePublisher>(sp =>
            new AndroidDownloadsLedgerPublisher(
                sp.GetRequiredService<Novolis.Avalonia.Mobile.IAppDataPaths>().RootDirectory));
        PresenceLedgerApp.Services = services.BuildServiceProvider();
        base.OnCreate();
    }

    /// <inheritdoc />
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).LogToTrace();
}
