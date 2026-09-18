using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Avalonia.Mobile.Android;
using Novolis.Manuscript.Export.Audio;
using ReadAloud;

namespace ReadAloud.Android;

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
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddNovolisMobileAndroid("ReadAloud");
                services.AddSingleton<AndroidMp3Player>();
                services.AddSingleton<IAudioPlayer>(sp => sp.GetRequiredService<AndroidMp3Player>());
                services.AddSingleton<IScreenWakeLock, AndroidScreenWakeLock>();
                services.AddReadAloudCore();
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
