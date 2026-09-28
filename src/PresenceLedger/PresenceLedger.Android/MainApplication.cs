using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Avalonia.Mobile.Android;
using PresenceLedger.App;
using PresenceLedgerApp = PresenceLedger.App.App;

namespace PresenceLedger.Android;

/// <summary>Android application host that initializes DI before Avalonia.</summary>
[Application]
public class MainApplication : AvaloniaAndroidApplication<PresenceLedgerApp>
{
    IHost? _host;

    /// <summary>JNI constructor.</summary>
    protected MainApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    /// <inheritdoc />
    public override void OnCreate()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddNovolisMobileAndroid("PresenceLedger");
                services.AddPresenceLedger();
            })
            .Build();
        PresenceLedgerApp.Services = _host.Services;
        _host.Start();
        base.OnCreate();
    }

    /// <inheritdoc />
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).LogToTrace();
}
