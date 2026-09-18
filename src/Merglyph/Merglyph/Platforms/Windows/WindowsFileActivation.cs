using Microsoft.Windows.AppLifecycle;
using Merglyph.Core;
using Windows.ApplicationModel.Activation;
using Windows.Storage;

namespace Merglyph;

internal static class WindowsFileActivation
{
    public static void Publish(AppActivationArguments? activation)
    {
        if (activation?.Kind != ExtendedActivationKind.File
            || activation.Data is not IFileActivatedEventArgs fileArgs
            || fileArgs.Files.OfType<StorageFile>().FirstOrDefault() is not { } file)
            return;

        ActivationBridge.Publish(new DocumentOpenRequest(
            new DocumentName(file.Name),
            async cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var randomAccessStream = await file.OpenAsync(FileAccessMode.Read);
                return randomAccessStream.AsStreamForRead();
            }));
    }

    public static void Publish(IEnumerable<string> arguments)
    {
        var path = arguments
            .Select(static argument => argument.Trim())
            .FirstOrDefault(static argument =>
                SupportedMarkdownDocuments.IsSupported(argument) && File.Exists(argument));

        if (path is null)
            return;

        var fullPath = Path.GetFullPath(path);
        ActivationBridge.Publish(new DocumentOpenRequest(
            new DocumentName(fullPath),
            cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Stream stream = new FileStream(fullPath, new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.Read,
                    Share = FileShare.ReadWrite | FileShare.Delete,
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                });
                return ValueTask.FromResult(stream);
            }));
    }
}
