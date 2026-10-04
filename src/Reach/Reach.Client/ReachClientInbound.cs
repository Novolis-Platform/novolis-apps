using Novolis.Transports.Framing;

namespace Novolis.Reach.Client;

internal sealed class ReachClientInbound(ReachClientSession session)
{
    internal async Task ReceiveLoopAsync(
        CancellationTokenSource owner,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var stream = session._stream;
                if (stream is null)
                    return;

                var frame = await LengthPrefixedFrameCodec.ReadAsync(
                        stream,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (frame is null)
                    return;

                if (!await ProcessAsync(ReachMessageCodec.Deserialize(frame.Payload))
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
            session.RaiseStatus($"Receive failed: {exception.Message}");
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested
                && Volatile.Read(ref session._disconnectRequested) == 0)
            {
                session.Closer.HandleUnexpectedDisconnect(owner);
            }
        }
    }

    internal async Task<bool> ProcessAsync(ReachMessageEnvelope envelope)
    {
        switch (envelope.Type)
        {
            case ReachMessageType.VideoStreamStart:
                session.RaiseVideoStreamStarted(
                    ReachMessageCodec.ReadBody<ReachVideoStreamStart>(envelope));
                break;
            case ReachMessageType.VideoStreamReset:
            {
                var reset = ReachMessageCodec.ReadBody<ReachVideoStreamReset>(envelope);
                Interlocked.Exchange(ref session._lastVideoSequence, reset.Sequence);
                session.RaiseVideoStreamReset(reset);
                break;
            }
            case ReachMessageType.DisplayTopology:
                session.RaiseDisplayTopology(
                    ReachMessageCodec.ReadBody<ReachDisplayTopology>(envelope));
                break;
            case ReachMessageType.VideoFrame:
            {
                var video = ReachMessageCodec.ReadBody<ReachVideoFrame>(envelope);
                Interlocked.Exchange(ref session._lastVideoSequence, video.Sequence);
                Interlocked.Exchange(
                    ref session._lastVideoFrameTicks,
                    DateTimeOffset.UtcNow.UtcTicks);
                session._performance.RecordReceived(
                    video.AccessUnit.Length,
                    video.Timestamp);
                session.RaisePerformanceChanged();
                session.SetState(ReachClientConnectionState.Streaming);
                session.SetPhase(ReachConnectionPhase.Streaming);
                session.RaiseVideoFrame(video);
                break;
            }
            case ReachMessageType.AudioStreamStart:
                session.RaiseAudioStreamStarted(
                    ReachMessageCodec.ReadBody<ReachAudioStreamStart>(envelope));
                break;
            case ReachMessageType.AudioFrame:
                session.RaiseAudioFrame(
                    ReachMessageCodec.ReadBody<ReachAudioFrame>(envelope));
                break;
            case ReachMessageType.DatagramOffer:
                await session.Media.ActivateDatagramAsync(
                        ReachMessageCodec.ReadBody<ReachDatagramOffer>(envelope))
                    .ConfigureAwait(false);
                break;
            case ReachMessageType.LatencyResponse:
            {
                var response = ReachMessageCodec.ReadBody<ReachLatencyResponse>(envelope);
                var elapsedTicks = DateTime.UtcNow.Ticks - response.SentUtcTicks;
                if (elapsedTicks >= 0)
                {
                    session._performance.RecordInputRoundTrip(
                        TimeSpan.FromTicks(elapsedTicks).TotalMilliseconds);
                    session.RaisePerformanceChanged();
                }

                break;
            }
            case ReachMessageType.SharingState:
                session.RaiseSharingState(
                    ReachMessageCodec.ReadBody<ReachSharingState>(envelope));
                break;
            case ReachMessageType.ClipboardContent:
                session.RaiseClipboardContent(
                    ReachMessageCodec.ReadBody<ReachClipboardContent>(envelope));
                break;
            case ReachMessageType.SessionClose:
            {
                var close = ReachMessageCodec.ReadBody<ReachSessionClose>(envelope);
                session.RaiseStatus($"The Reach host closed the session: {close.Reason}");
                Volatile.Write(ref session._remoteSessionEnded, 1);
                session.SetPhase(ReachConnectionPhase.Ended);
                session.RaiseSessionEnded(close.Reason);
                return false;
            }
        }

        return true;
    }
}
