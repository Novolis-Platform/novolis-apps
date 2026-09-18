using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Audio.Voice.Platform.Android;
using Novolis.Avalonia.Diagnostics;
using Novolis.Avalonia.Mobile.Android;
using Novolis.Logging.Diagnostics;
using Novolis.Manuscript.Export.Audio;
using ReadAloud;

namespace ReadAloud.Android;

[Application]
public class MainApplication : AvaloniaAndroidApplication<App>
{
    IHost? _host;
    DiagnosticJournal? _diagnostics;

    protected MainApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    public override void OnCreate()
    {
        var filesDirectory = global::Android.App.Application.Context?.FilesDir?.AbsolutePath
            ?? throw new InvalidOperationException("Android application files directory is unavailable.");
        _diagnostics = new DiagnosticJournal(new DiagnosticJournalOptions
        {
            DirectoryPath = Path.Combine(filesDirectory, "diagnostics"),
            ApplicationName = "ReadAloud",
        });
        AvaloniaDiagnostics.InstallEarly(_diagnostics);
        AndroidDiagnostics.InstallEarly(_diagnostics);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddDiagnosticFileLogging(_diagnostics);
                services.AddNovolisMobileAndroid("ReadAloud");
                services.AddNovolisMobileAndroidDiagnostics(_diagnostics);
                services.AddSingleton<AndroidMp3Player>();
                services.AddSingleton<IAudioPlayer>(sp => sp.GetRequiredService<AndroidMp3Player>());
                services.AddNovolisVoiceAndroid();
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
            .LogToTrace()
            .UseNovolisDiagnostics(_diagnostics
                ?? throw new InvalidOperationException("Diagnostics must be initialized before Avalonia setup."));
}
