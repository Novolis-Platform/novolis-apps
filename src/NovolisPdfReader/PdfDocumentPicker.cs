using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Platform;

namespace NovolisPdfReader;

/// <summary>Builds local-only open requests from the MAUI document picker.</summary>
public sealed class PdfDocumentPicker
{
    /// <summary>Shows the OS picker and returns a PDF request when selected.</summary>
    public async Task<PdfOpenRequest?> PickAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var file = await FilePicker.Default.PickAsync(
                new PickOptions
                {
                    PickerTitle = "Open a PDF document",
                    FileTypes = new FilePickerFileType(
                        new Dictionary<DevicePlatform, IEnumerable<string>>
                        {
                            [DevicePlatform.Android] = ["application/pdf"],
                            [DevicePlatform.WinUI] = [".pdf"],
                        }),
                })
            .ConfigureAwait(false);
        if (file is null)
            return null;

        var stableId = string.IsNullOrWhiteSpace(file.FullPath)
            ? file.FileName
            : file.FullPath;
        return new PdfOpenRequest(
            new PdfSourceDescriptor(file.FileName, stableId),
            async token =>
            {
                token.ThrowIfCancellationRequested();
                return await file.OpenReadAsync().ConfigureAwait(false);
            });
    }
}
