using Novolis.Reach.Transport;
using Novolis.Transports.Framing;

namespace Novolis.Reach.Client;

internal sealed class ReachClientConnector(ReachClientSession session)
{
    internal async Task ConnectAsync(
        string endpoint,
        ReachPlatform platform,
        string clientName,
        CancellationToken cancellationToken)
    {
        if (session.IsConnected)
            return;

        var transportEndpoint = ReachTransportEndpoint.Parse(endpoint);
        var mediaPort = session._mediaPort
            ?? (transportEndpoint.Address.Port == ReachProtocol.ControlPort
                ? ReachProtocol.MediaPort
                : checked(transportEndpoint.Address.Port + 1));
        Volatile.Write(ref session._disconnectRequested, 0);
        Volatile.Write(ref session._remoteSessionEnded, 0);
        session.SetState(ReachClientConnectionState.Connecting);
        session.SetPhase(ReachConnectionPhase.Connecting);
        session._lastEndpoint = endpoint;
        session._platform = platform;
        session._clientName = clientName;
        session.RaiseStatus(
            $"Connecting via {transportEndpoint.Scheme} to "
            + $"{transportEndpoint.Address.Address}:{transportEndpoint.Address.Port}...");
        IReachTransportConnection transport;
        try
        {
            transport = await ReachTransportConnector.ConnectAsync(
                    transportEndpoint,
                    mediaPort,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch when (
            transportEndpoint.IsQuic
            && !cancellationToken.IsCancellationRequested)
        {
            session.RaiseStatus("QUIC is unavailable; trying the TCP fallback...");
            transportEndpoint = transportEndpoint with
            {
                Scheme = "tcp",
                CertificatePin = null,
            };
            try
            {
                transport = await ReachTransportConnector.ConnectAsync(
                        transportEndpoint,
                        mediaPort,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                session.SetState(ReachClientConnectionState.Lost);
                session.RaiseStatus(
                    $"Unable to connect to {transportEndpoint.Address.Address}:"
                    + $"{transportEndpoint.Address.Port}.");
                throw;
            }
        }
        catch
        {
            session.SetState(ReachClientConnectionState.Lost);
            session.RaiseStatus(
                $"Unable to connect to {transportEndpoint.Address.Address}:"
                + $"{transportEndpoint.Address.Port}.");
            throw;
        }

        session._transport = transport;
        session._stream = transport.ControlStream;
        session.SetState(ReachClientConnectionState.Connected);
        session.SetPhase(ReachConnectionPhase.Authenticating);
        session.RaiseStatus(
            $"Connected via {transport.Info.Kind} to "
            + $"{transportEndpoint.Address.Address}:{transportEndpoint.Address.Port}; "
            + "negotiating...");

        try
        {
            await NegotiateAsync(platform, clientName, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            await session.Closer.CloseTransportAsync(announce: false)
                .ConfigureAwait(false);
            throw;
        }
    }

    internal async Task ReconnectAsync(CancellationToken cancellationToken)
    {
        if (session._lastEndpoint is null || session._clientName is null)
            throw new InvalidOperationException(
                "No previous Reach endpoint is available.");

        Volatile.Write(ref session._disconnectRequested, 0);
        session.SetPhase(ReachConnectionPhase.Reconnecting);
        await session.Closer.CloseTransportAsync(
                announce: true,
                preserveVideoSequence: true)
            .ConfigureAwait(false);
        await ConnectAsync(
                session._lastEndpoint,
                session._platform,
                session._clientName,
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal async Task RecoverMediaAsync(CancellationToken cancellationToken)
    {
        if (!session.IsConnected)
            throw new InvalidOperationException("Reach is not connected.");

        if (!session.IsMediaConnected && session._mediaPort != 0)
            await session.Media.TryConnectAsync(cancellationToken).ConfigureAwait(false);
        await session.RequestKeyFrameAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task NegotiateAsync(
        ReachPlatform platform,
        string clientName,
        CancellationToken cancellationToken)
    {
        await session.SendAsync(
            ReachMessageType.ClientHello,
            new ReachClientHello(
                ReachProtocol.AppId,
                ReachProtocol.Version,
                platform,
                clientName),
            cancellationToken).ConfigureAwait(false);
        await session.SendAsync(
            ReachMessageType.ClientCapabilities,
            new ReachCapabilitiesMessage(ReachClientIO.GetCapabilities(platform)),
            cancellationToken).ConfigureAwait(false);

        session.RaiseStatus("Waiting for Reach host capabilities...");
        var hostHello = await ReachClientIO.ReadAsync<ReachHostHello>(
                session,
                ReachMessageType.HostHello,
                cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(hostHello.AppId, ReachProtocol.AppId, StringComparison.Ordinal)
            || !ReachProtocol.IsCompatible(hostHello.ProtocolVersion))
        {
            throw new InvalidDataException(
                "The remote host does not speak Reach protocol 1.x.");
        }

        var hostCapabilities = await ReachClientIO.ReadAsync<ReachCapabilitiesMessage>(
                session,
                ReachMessageType.HostCapabilities,
                cancellationToken)
            .ConfigureAwait(false);
        var negotiatedCapabilities = ReachCapabilities.Intersect(
            hostCapabilities.Capabilities,
            ReachClientIO.GetCapabilities(platform));
        session.NegotiatedCapabilities = negotiatedCapabilities;
        session.SetPhase(ReachConnectionPhase.Starting);
        if (session._lastVideoSequence == 0)
        {
            await session.SendAsync(
                    ReachMessageType.SessionOpen,
                    new ReachSessionOpen(
                        session._sessionId,
                        string.Empty,
                        negotiatedCapabilities.Supports(ReachCapability.Audio)),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await session.SendAsync(
                    ReachMessageType.SessionResume,
                    new ReachSessionResume(
                        session._sessionId,
                        session._lastVideoSequence,
                        negotiatedCapabilities.Supports(ReachCapability.Audio)),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        session._receiveCancellation = new CancellationTokenSource();
        var receiveCancellation = session._receiveCancellation;
        session._receiveTask = Task.Run(
            () => session.Inbound.ReceiveLoopAsync(
                receiveCancellation,
                receiveCancellation.Token),
            CancellationToken.None);
        var latencyCancellation = new CancellationTokenSource();
        session._latencyCancellation = latencyCancellation;
        session._latencyTask = Task.Run(
            () => ProbeLatencyLoopAsync(latencyCancellation.Token),
            CancellationToken.None);
        await session.Media.TryConnectAsync(cancellationToken).ConfigureAwait(false);
        if (session._mediaPort != 0 && !session.IsMediaConnected)
        {
            session.RaiseStatus(
                $"Connected to {hostHello.HostName}, but the video channel is unavailable.");
            session.RaiseMediaConnectionLost();
        }

        session.RaiseStatus(
            session.IsMediaConnected || session._mediaPort == 0
                ? $"Connected to {hostHello.HostName}; "
                  + $"video={string.Join(",", negotiatedCapabilities.OfferedVideoCodecs)}"
                : $"Connected to {hostHello.HostName}, but the video channel is unavailable.");
        await session.RequestKeyFrameAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ProbeLatencyLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            while (await timer.WaitForNextTickAsync(cancellationToken)
                       .ConfigureAwait(false))
            {
                if (!session.IsConnected)
                    return;

                var probe = new ReachLatencyProbe(
                    Interlocked.Increment(ref session._sequence),
                    DateTime.UtcNow.Ticks);
                try
                {
                    await session.SendAsync(
                            ReachMessageType.LatencyProbe,
                            probe,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
