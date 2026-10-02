using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using Windows.Storage;

namespace Novolis.Ndjson.App;

internal static class WindowsFileActivation
{
    public static void Publish(AppActivationArguments? activation)
    {
        if (activation?.Kind != ExtendedActivationKind.File
            || activation.Data is not IFileActivatedEventArgs fileArguments
            || fileArguments.Files.OfType<StorageFile>().FirstOrDefault() is not { } file)
        {
            return;
        }

        ActivationBridge.Publish(NdjsonOpenRequest.FromStream(
            file.Name,
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
                Path.GetExtension(argument).Equals(".ndjson", StringComparison.OrdinalIgnoreCase)
                && File.Exists(argument));
        if (path is null)
            return;

        ActivationBridge.Publish(NdjsonOpenRequest.FromFile(new FileInfo(Path.GetFullPath(path))));
    }
}
