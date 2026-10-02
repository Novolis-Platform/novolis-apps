using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Audio.Voice.Platform.Android;
using Novolis.Avalonia.Diagnostics;
using Novolis.Avalonia.Mobile.Android;
using Novolis.Avalonia.Speech;
using Novolis.IO.Platform.Android;
using Novolis.Logging.Diagnostics;
using Novolis.Manuscript.Export.Audio;
using ReadAloud;
using ReadAloud.Services;

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
        _diagnostics = new DiagnosticJournal(new DiagnosticJournalOptions
        {
            DirectoryPath = Path.Combine(
                AndroidAppStorage.DefaultRoot("ReadAloud"),
                "diagnostics"),
            ApplicationName = "ReadAloud",
            StartupState = AndroidStartupState(),
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
                services.AddSingleton(_ => new AndroidEntraAuthentication(
                    SpeechService.DefaultAzureClientId,
                    SpeechService.DefaultAzureTenantId,
                    SpeechService.DefaultAzureLoginHint));
                services.AddSingleton<HttpClient>();
                services.AddSingleton<IAzureSpeechResourcePicker, AndroidAzureSpeechResourcePicker>();
                services.AddSingleton<IAzureSpeechCredentialFactory, AndroidEntraSpeechCredentialFactory>();
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

    static Dictionary<string, object?> AndroidStartupState()
    {
        var state = new Dictionary<string, object?>
        {
            ["platform"] = "Android",
            ["android.api"] = (int)Build.VERSION.SdkInt,
            ["device.manufacturer"] = Build.Manufacturer ?? string.Empty,
            ["device.model"] = Build.Model ?? string.Empty,
        };
        var manager = global::Android.App.Application.Context?.PackageManager;
        var packageName = global::Android.App.Application.Context?.PackageName;
        if (manager is null || string.IsNullOrWhiteSpace(packageName))
            return state;

        var info = manager.GetPackageInfo(packageName, (PackageInfoFlags)0);
        if (info is null)
            return state;

        state["app.version"] = info.VersionName ?? "0.0.0";
        state["app.build"] = OperatingSystem.IsAndroidVersionAtLeast(28)
            ? info.LongVersionCode
            : info.VersionCode;
        return state;
    }
}
