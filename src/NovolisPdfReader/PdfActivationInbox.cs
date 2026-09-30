using System.Threading.Channels;
using Novolis.Pdf.Platform;

namespace NovolisPdfReader;

/// <summary>Buffers OS PDF activations until the MAUI page is visible.</summary>
public sealed class PdfActivationInbox : IPdfActivationInbox
{
    private readonly Channel<PdfOpenRequest> _requests =
        Channel.CreateUnbounded<PdfOpenRequest>(
            new UnboundedChannelOptions { SingleReader = true });

    /// <inheritdoc />
    public bool Publish(PdfOpenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _requests.Writer.TryWrite(request);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<PdfOpenRequest> ReadAllAsync(
        CancellationToken cancellationToken = default) =>
        _requests.Reader.ReadAllAsync(cancellationToken);
}
