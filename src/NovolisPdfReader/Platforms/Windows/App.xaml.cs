using Microsoft.UI.Xaml;
using Microsoft.Maui;
using Microsoft.Windows.AppLifecycle;
using Novolis.Pdf.Abstractions;
using Novolis.Windows.Pdf;
using NovolisPdfReader;

namespace NovolisPdfReader.WinUI;

/// <summary>Windows activation bridge for PDF files and command-line opens.</summary>
public partial class App : MauiWinUIApplication
{
    /// <summary>Creates the Windows host.</summary>
    public App()
    {
        InitializeComponent();
        PdfReaderDiagnosticsLog.Shared.InstallProcessHooks();
        UnhandledException += (_, args) =>
        {
            PdfReaderDiagnosticsLog.Shared.Write(
                "winui-unhandled",
                args.Message ?? "WinUI unhandled exception.",
                exception: args.Exception);
            args.Handled = true;
        };
        PdfReaderDiagnosticsLog.Shared.Write(
            "windows-ctor",
            $"Windows host constructed. args={string.Join(" | ", Environment.GetCommandLineArgs())}");
    }

    /// <inheritdoc />
    protected override MauiApp CreateMauiApp() => NovolisPdfReader.MauiProgram.CreateMauiApp();

    /// <inheritdoc />
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        PdfReaderDiagnosticsLog.Shared.Write(
            "windows-launch",
            $"OnLaunched kind={activation?.Kind} arguments={string.Join(" | ", Environment.GetCommandLineArgs())}");
        base.OnLaunched(args);
        PublishActivation(activation);
        PublishCommandLine(Environment.GetCommandLineArgs().Skip(1));
    }

    private static void PublishActivation(AppActivationArguments? activation)
    {
        if (activation?.Kind != ExtendedActivationKind.File
            || activation.Data is not Windows.ApplicationModel.Activation.IFileActivatedEventArgs fileArgs
            || fileArgs.Files.OfType<Windows.Storage.StorageFile>().FirstOrDefault() is not { } file
            || !file.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            PdfReaderDiagnosticsLog.Shared.Write(
                "windows-file-activation",
                $"No file activation. kind={activation?.Kind}");
            return;
        }

        var request = WindowsPdfActivation.CreateRequest(
            new PdfSourceDescriptor(file.Name, file.Path),
            async cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var randomAccessStream = await file.OpenAsync(
                    Windows.Storage.FileAccessMode.Read);
                return randomAccessStream.AsStreamForRead();
            });
        var published = PdfActivationBridge.Publish(request);
        PdfReaderDiagnosticsLog.Shared.Write(
            "windows-file-activation",
            $"Published file activation published={published} path={file.Path}",
            request);
    }

    private static void PublishCommandLine(IEnumerable<string> arguments)
    {
        var request = WindowsPdfActivation.TryCreateRequest(arguments);
        if (request is null)
        {
            PdfReaderDiagnosticsLog.Shared.Write(
                "windows-command-line",
                $"No command-line PDF. remaining={string.Join(" | ", arguments)}");
            return;
        }

        var published = PdfActivationBridge.Publish(request);
        PdfReaderDiagnosticsLog.Shared.Write(
            "windows-command-line",
            $"Published command-line PDF published={published}",
            request);
    }
}
