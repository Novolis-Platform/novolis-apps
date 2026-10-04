namespace Novolis.Reach.Client;

internal sealed class ReachClientCloser(ReachClientSession session)
{
    internal async Task DisconnectAsync()
    {
        Volatile.Write(ref session._disconnectRequested, 1);
        if (session.IsConnected)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await session.SendAsync(
                        ReachMessageType.SessionClose,
                        new ReachSessionClose(session._sessionId, "Client disconnected."),
                        timeout.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        await CloseTransportAsync(announce: true).ConfigureAwait(false);
    }

    internal async Task CloseTransportAsync(
        bool announce,
        bool preserveVideoSequence = false)
    {
        var latencyCancellation = Interlocked.Exchange(
            ref session._latencyCancellation,
            null);
        var latencyTask = Interlocked.Exchange(ref session._latencyTask, null);
        latencyCancellation?.Cancel();
        var transport = Interlocked.Exchange(ref session._transport, null);
        var stream = Interlocked.Exchange(ref session._stream, null);
        var receiveCancellation = Interlocked.Exchange(
            ref session._receiveCancellation,
            null);
        var receiveTask = Interlocked.Exchange(ref session._receiveTask, null);
        receiveCancellation?.Cancel();
        stream?.Dispose();
        if (receiveTask is not null)
        {
            try
            {
                await receiveTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (
                receiveCancellation?.IsCancellationRequested == true)
            {
            }
        }

        receiveCancellation?.Dispose();
        if (latencyTask is not null)
        {
            try
            {
                await latencyTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (
                latencyCancellation?.IsCancellationRequested == true)
            {
            }
        }

        latencyCancellation?.Dispose();
        var datagramCancellation = Interlocked.Exchange(
            ref session._datagramReceiveCancellation,
            null);
        var datagramTask = Interlocked.Exchange(ref session._datagramReceiveTask, null);
        var datagramChannel = Interlocked.Exchange(ref session._datagramChannel, null);
        Interlocked.Exchange(ref session._datagramSession, null);
        Interlocked.Exchange(ref session._datagramEndpoint, null);
        datagramCancellation?.Cancel();
        if (datagramChannel is not null)
            await datagramChannel.DisposeAsync().ConfigureAwait(false);
        if (datagramTask is not null)
        {
            try
            {
                await datagramTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (
                datagramCancellation?.IsCancellationRequested == true)
            {
            }
        }

        datagramCancellation?.Dispose();
        var mediaStream = Interlocked.Exchange(ref session._mediaStream, null);
        var mediaCancellation = Interlocked.Exchange(
            ref session._mediaReceiveCancellation,
            null);
        var mediaTask = Interlocked.Exchange(ref session._mediaReceiveTask, null);
        mediaCancellation?.Cancel();
        mediaStream?.Dispose();
        if (mediaTask is not null)
        {
            try
            {
                await mediaTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (
                mediaCancellation?.IsCancellationRequested == true)
            {
            }
        }

        mediaCancellation?.Dispose();
        if (transport is not null)
            await transport.DisposeAsync().ConfigureAwait(false);
        session.NegotiatedCapabilities = null;
        if (!preserveVideoSequence)
            Interlocked.Exchange(ref session._lastVideoSequence, 0);
        Interlocked.Exchange(ref session._lastVideoFrameTicks, 0);
        Interlocked.Exchange(ref session._lastDatagramSequence, -1);
        Volatile.Write(ref session._remoteSessionEnded, 0);
        session.SetState(ReachClientConnectionState.Disconnected);
        session.SetPhase(ReachConnectionPhase.Disconnected);
        if (announce)
            session.RaiseStatus("Disconnected");
    }

    internal void HandleUnexpectedDisconnect(CancellationTokenSource owner)
    {
        if (!ReferenceEquals(
                Interlocked.CompareExchange(
                    ref session._receiveCancellation,
                    null,
                    owner),
                owner))
        {
            return;
        }

        _ = Interlocked.Exchange(ref session._receiveTask, null);
        Interlocked.Exchange(ref session._stream, null)?.Dispose();
        var transport = Interlocked.Exchange(ref session._transport, null);
        var datagramOwner = Interlocked.Exchange(
            ref session._datagramReceiveCancellation,
            null);
        var datagramChannel = Interlocked.Exchange(ref session._datagramChannel, null);
        _ = Interlocked.Exchange(ref session._datagramReceiveTask, null);
        Interlocked.Exchange(ref session._datagramSession, null);
        Interlocked.Exchange(ref session._datagramEndpoint, null);
        datagramOwner?.Cancel();
        try
        {
            datagramChannel?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch
        {
        }

        var mediaOwner = Interlocked.Exchange(
            ref session._mediaReceiveCancellation,
            null);
        Interlocked.Exchange(ref session._latencyCancellation, null)?.Cancel();
        _ = Interlocked.Exchange(ref session._latencyTask, null);
        mediaOwner?.Cancel();
        Interlocked.Exchange(ref session._mediaStream, null)?.Dispose();
        try
        {
            transport?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch
        {
        }

        session.NegotiatedCapabilities = null;
        Interlocked.Exchange(ref session._lastVideoSequence, 0);
        Interlocked.Exchange(ref session._lastVideoFrameTicks, 0);
        Interlocked.Exchange(ref session._lastDatagramSequence, -1);
        owner.Dispose();
        mediaOwner?.Dispose();
        session.SetState(ReachClientConnectionState.Lost);
        session.SetPhase(
            Volatile.Read(ref session._remoteSessionEnded) != 0
                ? ReachConnectionPhase.Ended
                : ReachConnectionPhase.Reconnecting);
        session.RaiseStatus("Reach connection lost. Use reconnect to resume.");
        session.RaiseConnectionLost();
    }
}
