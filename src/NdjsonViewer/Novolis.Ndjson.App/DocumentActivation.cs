using System.Threading.Channels;

namespace Novolis.Ndjson.App;

public sealed class DocumentActivationInbox
{
    private readonly Channel<NdjsonOpenRequest> _channel =
        Channel.CreateUnbounded<NdjsonOpenRequest>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
            });

    public bool Publish(NdjsonOpenRequest request) => _channel.Writer.TryWrite(request);

    public IAsyncEnumerable<NdjsonOpenRequest> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
