using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using BooksMobile;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Manuscript.Export.Audio;
using Novolis.Avalonia.Mobile.Android;

namespace BooksMobile.Android;

[Application]
public class MainApplication : AvaloniaAndroidApplication<App>
{
    IHost? _host;

    protected MainApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    public override void OnCreate()
    {
        // DI must be ready before Avalonia Setup / OnFrameworkInitializationCompleted.
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddNovolisMobileAndroid("BooksMobile");
                services.AddSingleton<AndroidMp3Player>();
                services.AddSingleton<IAudioPlayer>(sp => sp.GetRequiredService<AndroidMp3Player>());
                services.AddSingleton<IScreenWakeLock, AndroidScreenWakeLock>();
                services.AddBooksMobileCore();
            })
            .Build();
        App.Services = _host.Services;
        _host.Start();

        base.OnCreate();
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder)
            .LogToTrace();
}
