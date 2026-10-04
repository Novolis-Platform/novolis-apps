using System.Net;
using Novolis.Reach.Transport;
using Novolis.Transports;
using Novolis.Transports.Framing;

namespace Novolis.Reach.Host.Server;

internal sealed class ReachHostClientConnection : IAsyncDisposable
{
    private readonly ITransportConnection _transport;
    private readonly ITransportStream _controlTransportStream;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly ReachHostClientMedia _media;
    private int _disposeStarted;

    private ReachHostClientConnection(
        long id,
        ITransportConnection transport,
        ITransportStream controlTransportStream)
    {
        Id = id;
        _transport = transport;
        _controlTransportStream = controlTransportStream;
        _media = new ReachHostClientMedia(this, transport);
    }

    internal static async ValueTask<ReachHostClientConnection> CreateAsync(
        long id,
        ITransportConnection transport,
        CancellationToken cancellationToken)
    {
        var stream = await transport.AcceptInboundStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        return new ReachHostClientConnection(id, transport, stream);
    }

    internal long Id { get; }
    internal ITransportConnection Transport => _transport;
    internal Stream Stream => _controlTransportStream.Stream;
    internal EndPoint RemoteEndPoint => _transport.Info.RemoteEndPoint;
    internal bool IsReady { get; set; }
    internal ReachCapabilities? Capabilities { get; set; }
    internal Guid SessionId { get; set; }
    internal string RequestedDisplayId { get; set; } = string.Empty;
    internal long LastVideoSequence { get; set; }
    internal bool EnableAudio { get; set; }
    internal bool SessionCloseForwarded { get; set; }
    internal bool HasMediaChannel => _media.HasMediaChannel;
    internal ReachDatagramSession? DatagramSession => _media.DatagramSession;

    internal void SetDatagramSession(ReachDatagramSession session) =>
        _media.SetDatagramSession(session);

    internal void AttachDatagram(
        ITransportDatagramChannel channel,
        EndPoint remoteEndpoint) =>
        _media.AttachDatagram(channel, remoteEndpoint);

    internal void ClearDatagram() => _media.ClearDatagram();

    internal void AttachMediaStream(
        ITransportConnection owner,
        ITransportStream stream) =>
        _media.AttachMediaStream(owner, stream);

    internal void DetachMediaStream(Stream stream) =>
        _media.DetachMediaStream(stream);

    internal async ValueTask SendAsync<T>(
        ReachMessageType type,
        T message,
        CancellationToken cancellationToken)
    {
        await SendPayloadAsync(
            ReachMessageCodec.Serialize(
                type,
                DateTime.UtcNow.Ticks,
                message),
            cancellationToken).ConfigureAwait(false);
    }

    internal async ValueTask SendPayloadAsync(
        byte[] payload,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await LengthPrefixedFrameCodec.WriteAsync(
                    Stream,
                    payload,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    internal ValueTask<bool> SendPayloadForChannelAsync(
        byte[] payload,
        bool media,
        bool latestFrame,
        CancellationToken cancellationToken) =>
        _media.SendPayloadForChannelAsync(
            payload,
            media,
            latestFrame,
            cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
            return;

        await _media.DisposeAsync().ConfigureAwait(false);
        await _controlTransportStream.DisposeAsync().ConfigureAwait(false);
        await _transport.DisposeAsync().ConfigureAwait(false);
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposeStarted) != 0)
            throw new ObjectDisposedException(nameof(ReachHostClientConnection));
    }
}
