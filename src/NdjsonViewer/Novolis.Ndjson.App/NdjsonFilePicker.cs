using Microsoft.Maui.Storage;

namespace Novolis.Ndjson.App;

public sealed class NdjsonFilePicker
{
    private static readonly FilePickerFileType NdjsonTypes = new(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.Android] = ["application/x-ndjson", "application/json", "text/plain"],
            [DevicePlatform.WinUI] = [".ndjson"],
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

        if (!Path.GetExtension(result.FileName).Equals(".ndjson", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Novolis NDJSON Viewer opens .ndjson files only.");

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
