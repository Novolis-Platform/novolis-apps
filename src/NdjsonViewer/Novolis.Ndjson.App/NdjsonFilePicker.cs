using Microsoft.Maui.Storage;

namespace Novolis.Ndjson.App;

public sealed class NdjsonFilePicker
{
    private static readonly FilePickerFileType NdjsonTypes = new(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.Android] =
            [
                "application/x-ndjson",
                "application/ndjson",
                "text/x-ndjson",
                "application/json",
                "text/plain",
                "application/octet-stream",
            ],
            [DevicePlatform.WinUI] = [".ndjson", ".jsonl"],
        });

    public async ValueTask<NdjsonOpenRequest?> PickAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await FilePicker.Default.PickAsync(
            new PickOptions
            {
                PickerTitle = "Open NDJSON file",
                FileTypes = NdjsonTypes,
            }).ConfigureAwait(false);

        if (result is null)
            return null;

        var extension = Path.GetExtension(result.FileName);
        if (!extension.Equals(".ndjson", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".jsonl", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Novolis NDJSON Viewer opens .ndjson and .jsonl files only.");
        }

        if (!OperatingSystem.IsAndroid()
            && !string.IsNullOrWhiteSpace(result.FullPath)
            && File.Exists(result.FullPath))
        {
            return NdjsonOpenRequest.FromFile(new FileInfo(result.FullPath));
        }

        return NdjsonOpenRequest.FromStream(
            result.FileName,
            async token =>
            {
                token.ThrowIfCancellationRequested();
                return await result.OpenReadAsync().ConfigureAwait(false);
            });
    }
}
