using System.Threading.Channels;
using Merglyph.Core;

namespace Merglyph;

public delegate ValueTask<Stream> OpenDocumentStream(CancellationToken cancellationToken);

public sealed record DocumentOpenRequest(DocumentName Name, OpenDocumentStream OpenReadAsync);

public sealed class DocumentActivationInbox
{
    private readonly Channel<DocumentOpenRequest> _channel = Channel.CreateUnbounded<DocumentOpenRequest>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public bool Publish(DocumentOpenRequest request) => _channel.Writer.TryWrite(request);

    public IAsyncEnumerable<DocumentOpenRequest> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}

public static class ActivationBridge
{
    private static readonly object Gate = new();
    private static readonly Queue<DocumentOpenRequest> Pending = new();
    private static DocumentActivationInbox? _inbox;

    public static void Initialize(DocumentActivationInbox inbox)
    {
        ArgumentNullException.ThrowIfNull(inbox);
        lock (Gate)
        {
            _inbox = inbox;
            while (Pending.TryDequeue(out var request))
                inbox.Publish(request);
        }
    }

    public static void Publish(DocumentOpenRequest request)
    {
        lock (Gate)
        {
            if (_inbox is null)
                Pending.Enqueue(request);
            else
                _inbox.Publish(request);
        }
    }
}

public sealed class DocumentSession(MarkdownDocumentReader reader)
{
    public MarkdownDocument? Current { get; private set; }

    public async ValueTask<MarkdownDocument> OpenAsync(
        DocumentOpenRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var stream = await request.OpenReadAsync(cancellationToken);
        Current = await reader.ReadAsync(request.Name, stream, cancellationToken);
        return Current;
    }
}
