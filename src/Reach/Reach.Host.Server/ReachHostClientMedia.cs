using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Novolis.Reach.Transport;
using Novolis.Transports;
using Novolis.Transports.Framing;

namespace Novolis.Reach.Host.Server;

internal sealed class ReachHostClientMedia : IAsyncDisposable
{
    private readonly ReachHostClientConnection _owner;
    private readonly ITransportConnection _controlTransport;
    private readonly SemaphoreSlim _mediaSendGate = new(1, 1);
    private readonly object _mediaStateGate = new();
    private readonly Channel<byte[]> _latestMedia =
        Channel.CreateBounded<byte[]>(
            new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            });
    private readonly CancellationTokenSource _mediaLifetime = new();
    private readonly Task _mediaSendTask;
    private ITransportStream? _mediaTransportStream;
    private ITransportConnection? _mediaTransport;
    private Stream? _mediaStream;
    private ITransportDatagramChannel? _datagramChannel;
    private IPEndPoint? _datagramEndpoint;
    private ReachDatagramSession? _datagramSession;
    private long _datagramSequence;
    private int _disposeStarted;

    internal ReachHostClientMedia(
        ReachHostClientConnection owner,
        ITransportConnection controlTransport)
    {
        _owner = owner;
        _controlTransport = controlTransport;
        _mediaSendTask = Task.Run(
            () => MediaSendLoopAsync(_mediaLifetime.Token));
    }

    internal bool HasMediaChannel => Volatile.Read(ref _mediaStream) is not null;

    internal ReachDatagramSession? DatagramSession =>
        Volatile.Read(ref _datagramSession);

    internal void SetDatagramSession(ReachDatagramSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        ClearDatagram();
        Volatile.Write(ref _datagramSession, session);
    }

    internal void AttachDatagram(
        ITransportDatagramChannel channel,
        EndPoint remoteEndpoint)
    {
        ArgumentNullException.ThrowIfNull(channel);
        if (remoteEndpoint is not IPEndPoint ipEndpoint)
            return;

        Volatile.Write(ref _datagramChannel, channel);
        Volatile.Write(
            ref _datagramEndpoint,
            new IPEndPoint(ipEndpoint.Address, ipEndpoint.Port));
    }

    internal void ClearDatagram()
    {
        Volatile.Write(ref _datagramEndpoint, null);
        Volatile.Write(ref _datagramChannel, null);
        Volatile.Write(ref _datagramSession, null);
    }

    internal void AttachMediaStream(
        ITransportConnection owner,
        ITransportStream stream)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(stream);
        ITransportStream? previousStream;
        ITransportConnection? previousTransport;
        lock (_mediaStateGate)
        {
            previousStream = _mediaTransportStream;
            previousTransport = _mediaTransport;
            _mediaTransportStream = stream;
            _mediaTransport = ReferenceEquals(owner, _controlTransport)
                ? null
                : owner;
            _mediaStream = stream.Stream;
        }

        previousStream?.Stream.Dispose();
        DisposeSynchronously(previousTransport);
    }

    internal void DetachMediaStream(Stream stream)
    {
        ITransportStream? transportStream = null;
        ITransportConnection? transport = null;
        lock (_mediaStateGate)
        {
            if (!ReferenceEquals(_mediaStream, stream))
                return;

            _mediaStream = null;
            transportStream = _mediaTransportStream;
            transport = _mediaTransport;
            _mediaTransportStream = null;
            _mediaTransport = null;
        }

        transportStream?.Stream.Dispose();
        DisposeSynchronously(transport);
    }

    internal async ValueTask<bool> SendPayloadForChannelAsync(
        byte[] payload,
        bool media,
        bool latestFrame,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var datagramSession = Volatile.Read(ref _datagramSession);
        var datagramChannel = Volatile.Read(ref _datagramChannel);
        var datagramEndpoint = Volatile.Read(ref _datagramEndpoint);
        if (media
            && datagramSession is not null
            && datagramChannel is not null
            && datagramEndpoint is not null)
        {
            try
            {
                var sequence = Interlocked.Increment(ref _datagramSequence);
                foreach (var packet in ReachDatagramPacketCodec.EncodeData(
                             datagramSession,
                             sequence,
                             payload,
                             datagramChannel.MaximumPayloadSize))
                {
                    await datagramChannel.SendAsync(
                            packet,
                            datagramEndpoint,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                return false;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                ClearDatagram();
            }
        }

        var mediaStream = Volatile.Read(ref _mediaStream);
        if (!media || mediaStream is null)
        {
            await _owner.SendPayloadAsync(payload, cancellationToken)
                .ConfigureAwait(false);
            return false;
        }

        if (latestFrame)
        {
            if (_latestMedia.Writer.TryWrite(payload))
                return false;

            var dropped = _latestMedia.Reader.TryRead(out _);
            if (!_latestMedia.Writer.TryWrite(payload))
                dropped = true;
            return dropped;
        }

        await _mediaSendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await LengthPrefixedFrameCodec.WriteAsync(
                    mediaStream,
                    payload,
                    cancellationToken)
                .ConfigureAwait(false);
            return false;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            DetachMediaStream(mediaStream);
            await _owner.SendPayloadAsync(payload, cancellationToken)
                .ConfigureAwait(false);
            return false;
        }
        finally
        {
            _mediaSendGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
            return;

        _latestMedia.Writer.TryComplete();
        _mediaLifetime.Cancel();
        ITransportStream? mediaTransportStream;
        ITransportConnection? mediaTransport;
        lock (_mediaStateGate)
        {
            mediaTransportStream = _mediaTransportStream;
            mediaTransport = _mediaTransport;
            _mediaTransportStream = null;
            _mediaTransport = null;
            _mediaStream = null;
        }

        ClearDatagram();
        if (mediaTransportStream is not null)
            await mediaTransportStream.DisposeAsync().ConfigureAwait(false);
        if (mediaTransport is not null)
            await mediaTransport.DisposeAsync().ConfigureAwait(false);
        try
        {
            await _mediaSendTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_mediaLifetime.IsCancellationRequested)
        {
        }

        _mediaLifetime.Dispose();
    }

    private async Task MediaSendLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var payload in _latestMedia.Reader.ReadAllAsync(
                               cancellationToken))
            {
                var mediaStream = Volatile.Read(ref _mediaStream);
                if (mediaStream is null)
                    continue;

                await _mediaSendGate.WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    await LengthPrefixedFrameCodec.WriteAsync(
                            mediaStream,
                            payload,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception) when (
                    !cancellationToken.IsCancellationRequested)
                {
                    DetachMediaStream(mediaStream);
                }
                finally
                {
                    _mediaSendGate.Release();
                }
            }
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposeStarted) != 0)
            throw new ObjectDisposedException(nameof(ReachHostClientConnection));
    }

    private static void DisposeSynchronously(IAsyncDisposable? disposable)
    {
        if (disposable is null)
            return;

        try
        {
            disposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch
        {
        }
    }
}
