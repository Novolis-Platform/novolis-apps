using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Avalonia.Mobile.Android;
using Novolis.IO.Platform.Android;
using PresenceLedger.App;
using PresenceLedger.Core;
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
        var locations = AndroidAppStorage.Open(
            global::Android.App.Application.Context
                ?? throw new InvalidOperationException("Android application context is unavailable."),
            "PresenceLedger",
            AndroidStoragePreference.AppPrivate);
        var storageStatus = new LedgerStorageStatus(
            locations.Kind.ToString(),
            locations.ImportResult.FilesCopied,
            locations.ImportResult.ErrorMessage);
        services.AddNovolisMobileAndroid("PresenceLedger");
        services.AddSingleton(locations);
        services.AddSingleton<Novolis.Avalonia.Mobile.IAppDataPaths>(
            new AndroidAppDataPaths(locations));
        services.AddSingleton<ILedgerStorageStatus>(storageStatus);
        services.AddPresenceLedger();
        services.AddSingleton<AndroidLegacyLedgerImport>(sp =>
            new AndroidLegacyLedgerImport(
                global::Android.App.Application.Context
                    ?? throw new InvalidOperationException("Android application context is unavailable."),
                sp.GetRequiredService<Novolis.Avalonia.Mobile.IAppDataPaths>().RootDirectory,
                sp.GetRequiredService<IPresenceDiagnosticsStore>(),
                storageStatus,
                () => MainActivity.Current));
        services.AddSingleton<ILedgerStorageRecovery>(sp =>
            sp.GetRequiredService<AndroidLegacyLedgerImport>());
        services.AddSingleton<AndroidLedgerBundleTransfer>(sp =>
            new AndroidLedgerBundleTransfer(
                global::Android.App.Application.Context
                    ?? throw new InvalidOperationException("Android application context is unavailable."),
                () => MainActivity.Current));
        services.AddSingleton<ILedgerBundleTransfer>(sp =>
            sp.GetRequiredService<AndroidLedgerBundleTransfer>());
        services.AddSingleton<ILedgerFilePublisher>(sp =>
            new AndroidDownloadsLedgerPublisher(
                sp.GetRequiredService<Novolis.Avalonia.Mobile.IAppDataPaths>().RootDirectory));
        services.AddSingleton<ILedgerFileShare>(sp =>
            new AndroidLedgerShare(
                sp.GetRequiredService<Novolis.Avalonia.Mobile.IAppDataPaths>().RootDirectory));
        services.AddSingleton<IReportExport>(sp =>
            new AndroidReportExport());
        var provider = services.BuildServiceProvider();
        PresenceLedgerApp.Services = provider;
        try
        {
            provider.GetRequiredService<IPresenceDiagnosticsStore>()
                .AppendAsync(
                    new PresenceDiagnosticRecord(
                        DateTimeOffset.UtcNow,
                        locations.ImportResult.ErrorMessage is null ? "info" : "warning",
                        "storage-resolved",
                        $"Resolved {locations.Kind} ledger storage.",
                        locations.ImportResult.ErrorMessage
                            ?? $"Imported {locations.ImportResult.FilesCopied} legacy file(s)."))
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
            // A startup diagnostic failure must not prevent the host from opening.
        }
        base.OnCreate();
    }

    /// <inheritdoc />
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).LogToTrace();
}
