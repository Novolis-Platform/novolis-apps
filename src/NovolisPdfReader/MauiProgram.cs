using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Novolis.Maui.GraphicalProfile;
using Novolis.Maui.PdfViewer;
using Novolis.Pdf.Documents;
#if ANDROID
using Novolis.IO.Platform.Android;
#endif

namespace NovolisPdfReader;

/// <summary>Composes the thin Novolis PDF Reader MAUI host.</summary>
public static class MauiProgram
{
    /// <summary>Builds the local-only application.</summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.UseGraphicalProfile();
#if WINDOWS
        builder.ConfigureMauiHandlers(static handlers => WinUiButtonChrome.Map(handlers));
#endif
        builder.Services
            .AddSingleton(_ => PdfReaderDiagnosticsLog.Shared)
            .AddSingleton<PdfActivationInbox>()
            .AddSingleton<PdfDocumentPicker>()
            .AddSingleton<IPdfDocumentStateStore>(
                _ => new JsonPdfDocumentStateStore(
                    Path.Combine(AppStorageRoot(), "reader-state")))
            .AddSingleton<PdfViewer>()
            .AddSingleton<MainPage>();
        PdfReaderDiagnosticsLog.Shared.InstallProcessHooks();
        PdfReaderDiagnosticsLog.Shared.Write("maui-compose", "MAUI host composed.");
        var app = builder.Build();
        PdfActivationBridge.Initialize(app.Services.GetRequiredService<PdfActivationInbox>());
        return app;
    }

    static string AppStorageRoot()
    {
#if ANDROID
        return AndroidAppStorage.DefaultRoot("NovolisPdfReader");
#else
        return FileSystem.Current.AppDataDirectory;
#endif
    }
}
