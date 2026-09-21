using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Avalonia.Mobile.Android;
using Novolis.Reach.Client;

namespace Novolis.Reach.Client.Android;

[Application]
public class MainApplication : AvaloniaAndroidApplication<App>
{
    private IHost? _host;

    protected MainApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    public override void OnCreate()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddNovolisMobileAndroid("ReachClientAndroid");
                services.AddSingleton<ReachClientSession>();
                services.AddSingleton<ReachClientView>();
            })
            .Build();
        App.Services = _host.Services;
        _host.Start();
        base.OnCreate();
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).LogToTrace();
}
