using System.Net;
using Novolis.Reach.Transport;
using Novolis.Transports;
using Novolis.Transports.Framing;
using Novolis.Transports.Udp;

namespace Novolis.Reach.Client;

internal sealed class ReachClientMediaChannel(ReachClientSession session)
{
    internal async Task TryConnectAsync(CancellationToken cancellationToken)
    {
        if (session._mediaPort == 0 || session._transport is null)
            return;

        Stream? stream = null;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(1));
            stream = await session._transport.OpenMediaStreamAsync(timeout.Token)
                .ConfigureAwait(false);
            await LengthPrefixedFrameCodec.WriteAsync(
                    stream,
                    ReachMessageCodec.Serialize(
                        ReachMessageType.MediaHello,
                        Interlocked.Increment(ref session._sequence),
                        new ReachMediaHello(
                            session._sessionId,
                            ReachProtocol.AppId,
                            ReachProtocol.Version)),
                    timeout.Token)
                .ConfigureAwait(false);

            session._mediaStream = stream;
            session._mediaReceiveCancellation = new CancellationTokenSource();
            var receiveCancellation = session._mediaReceiveCancellation;
            session._mediaReceiveTask = Task.Run(
                () => ReceiveMediaLoopAsync(
                    receiveCancellation,
                    receiveCancellation.Token),
                CancellationToken.None);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stream?.Dispose();
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            stream?.Dispose();
        }
    }

    internal async Task ActivateDatagramAsync(ReachDatagramOffer offer)
    {
        if (!session.IsConnected
            || offer.SessionId != session._sessionId
            || offer.Port is <= 0 or > 65535
            || Volatile.Read(ref session._datagramReceiveTask) is not null)
        {
            return;
        }

        UdpDatagramChannel? channel = null;
        try
        {
            var endpoint = ReachTransportEndpoint.Parse(
                session._lastEndpoint
                ?? throw new InvalidOperationException(
                    "The Reach endpoint is no longer available."));
            var key = Convert.FromBase64String(offer.Key);
            var sessionOffer = new ReachDatagramSession(
                offer.SessionId,
                key,
                offer.Token);
            sessionOffer.Validate();
            channel = new UdpDatagramChannel(
                new IPEndPoint(IPAddress.Any, 0),
                new UdpDatagramChannelOptions
                {
                    MaximumPayloadSize = Math.Min(
                        ReachDatagramPacketCodec.MaximumPacketSize,
                        offer.MaximumPacketSize),
                    ReceiveQueueCapacity = 128,
                    ReceiveQueueFullMode =
                        System.Threading.Channels.BoundedChannelFullMode.DropOldest,
                });
            var remoteEndpoint = new IPEndPoint(endpoint.Address.Address, offer.Port);
            using var handshakeTimeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(1));
            await channel.SendAsync(
                    ReachDatagramPacketCodec.EncodeHandshake(
                        sessionOffer.SessionId,
                        sessionOffer.Token),
                    remoteEndpoint,
                    handshakeTimeout.Token)
                .ConfigureAwait(false);

            var receiveCancellation = new CancellationTokenSource();
            if (Interlocked.CompareExchange(
                    ref session._datagramChannel,
                    channel,
                    null) is not null)
            {
                receiveCancellation.Dispose();
                await channel.DisposeAsync().ConfigureAwait(false);
                return;
            }

            channel = null;
            Volatile.Write(ref session._datagramSession, sessionOffer);
            Volatile.Write(ref session._datagramEndpoint, remoteEndpoint);
            Volatile.Write(ref session._datagramReceiveCancellation, receiveCancellation);
            var receiveTask = Task.Run(
                () => ReceiveDatagramLoopAsync(
                    receiveCancellation,
                    receiveCancellation.Token),
                CancellationToken.None);
            Volatile.Write(ref session._datagramReceiveTask, receiveTask);
            session.RaiseStatus(
                $"Authenticated UDP media is active on local port "
                + $"{((UdpDatagramChannel)session._datagramChannel).LocalEndPoint.Port}.");
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException)
        {
            channel?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            session.RaiseStatus(
                $"UDP media activation failed; using the reliable fallback: {exception.Message}");
        }
    }

    internal void Detach(CancellationTokenSource owner)
    {
        if (!ReferenceEquals(
                Interlocked.CompareExchange(
                    ref session._mediaReceiveCancellation,
                    null,
                    owner),
                owner))
        {
            return;
        }

        _ = Interlocked.Exchange(ref session._mediaReceiveTask, null);
        Interlocked.Exchange(ref session._mediaStream, null)?.Dispose();
        owner.Dispose();
    }

    private async Task ReceiveDatagramLoopAsync(
        CancellationTokenSource owner,
        CancellationToken cancellationToken)
    {
        try
        {
            var channel = Volatile.Read(ref session._datagramChannel);
            var datagramSession = Volatile.Read(ref session._datagramSession);
            if (channel is null || datagramSession is null)
                return;

            var reassembler = new ReachDatagramReassembler();
            while (!cancellationToken.IsCancellationRequested)
            {
                var datagram = await channel.ReceiveAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (!ReachDatagramPacketCodec.TryDecodeData(
                        datagramSession,
                        datagram.Payload,
                        out var fragment)
                    || !Equals(datagram.RemoteEndpoint, session._datagramEndpoint))
                {
                    continue;
                }

                if (!reassembler.TryAccept(fragment, datagram.ReceivedAt, out var payload))
                    continue;

                var previous = Interlocked.Exchange(
                    ref session._lastDatagramSequence,
                    fragment.Sequence);
                if (previous >= 0 && fragment.Sequence > previous + 1)
                    await RequestKeyFrameAfterGapAsync().ConfigureAwait(false);

                if (!await session.Inbound.ProcessAsync(
                            ReachMessageCodec.Deserialize(payload))
                        .ConfigureAwait(false))
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException) when (
            cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            session.RaiseStatus(
                $"UDP media receive failed; using the reliable fallback: {exception.Message}");
        }
        finally
        {
            if (ReferenceEquals(
                    Interlocked.CompareExchange(
                        ref session._datagramReceiveCancellation,
                        null,
                        owner),
                    owner))
            {
                _ = Interlocked.Exchange(ref session._datagramReceiveTask, null);
                Interlocked.Exchange(ref session._datagramChannel, null);
                Interlocked.Exchange(ref session._datagramSession, null);
                Interlocked.Exchange(ref session._datagramEndpoint, null);
                owner.Dispose();
                if (session.IsConnected)
                    session.RaiseStatus(
                        "UDP media ended; the reliable fallback remains available.");
            }
        }
    }

    private async Task ReceiveMediaLoopAsync(
        CancellationTokenSource owner,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var stream = session._mediaStream;
                if (stream is null)
                    return;

                var frame = await LengthPrefixedFrameCodec.ReadAsync(
                        stream,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (frame is null)
                    return;

                if (!await session.Inbound.ProcessAsync(
                            ReachMessageCodec.Deserialize(frame.Payload))
                        .ConfigureAwait(false))
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            session.RaiseStatus($"Media receive failed: {exception.Message}");
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Detach(owner);
                if (session.IsConnected)
                    session.SetState(ReachClientConnectionState.Connected);
                if (session.IsConnected)
                    session.SetPhase(ReachConnectionPhase.Degraded);
                if (!session.IsDatagramConnected)
                {
                    session.RaiseStatus("The Reach media stream ended.");
                    session.RaiseMediaConnectionLost();
                }
            }
        }
    }

    private async Task RequestKeyFrameAfterGapAsync()
    {
        try
        {
            await session.RequestKeyFrameAsync().ConfigureAwait(false);
        }
        catch
        {
        }
    }
}
