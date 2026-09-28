using System.Net;
using System.Net.Sockets;
using System.Linq;
using Novolis.Reach.Client;
using Novolis.Reach.Protocol;
using Novolis.Transports.Framing;

namespace Reach.Unit;

public sealed class ReachClientSessionTests
{
    [Test]
    public async Task ClientSessionCompletesLocalEmulatorHandshakeAndReceivesInput()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var inputReceived = new TaskCompletionSource<ReachPointerMove>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var videoReceived = new TaskCompletionSource<ReachVideoFrame>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var emulator = RunEmulatorAsync(
            listener,
            inputReceived,
            cancellation.Token);

        await using var session = new ReachClientSession();
        session.VideoFrameReceived += frame => videoReceived.TrySetResult(frame);
        await session.ConnectAsync(
                $"tcp://127.0.0.1:{endpoint.Port}",
                ReachPlatform.Windows,
                "Reach.Unit",
                cancellation.Token)
            .ConfigureAwait(false);

        var video = await videoReceived.Task.WaitAsync(cancellation.Token);
        await Assert.That(video.Codec).IsEqualTo("H264");
        await Assert.That(video.IsKeyFrame).IsTrue();
        await Assert.That(video.AccessUnit.SequenceEqual(new byte[] { 1, 2, 3 })).IsTrue();

        await session.SendPointerMoveAsync(42, 24, cancellation.Token);
        var input = await inputReceived.Task.WaitAsync(cancellation.Token);
        await Assert.That(input.X).IsEqualTo(42);
        await Assert.That(input.Y).IsEqualTo(24);

        await session.DisconnectAsync().ConfigureAwait(false);
        await emulator.WaitAsync(cancellation.Token).ConfigureAwait(false);
    }

    private static async Task RunEmulatorAsync(
        TcpListener listener,
        TaskCompletionSource<ReachPointerMove> inputReceived,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken)
                .ConfigureAwait(false);
            using var stream = client.GetStream();

            var hello = await ReadBodyAsync<ReachClientHello>(
                    stream,
                    cancellationToken)
                .ConfigureAwait(false);
            var capabilities = await ReadBodyAsync<ReachCapabilitiesMessage>(
                    stream,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendAsync(
                    stream,
                    ReachMessageType.HostHello,
                    new ReachHostHello(
                        ReachProtocol.AppId,
                        ReachProtocol.Version,
                        "Reach.Unit Emulator",
                        ["tcp://127.0.0.1:19800"]),
                    cancellationToken)
                .ConfigureAwait(false);
            await SendAsync(
                    stream,
                    ReachMessageType.HostCapabilities,
                    new ReachCapabilitiesMessage(
                        ReachCapabilities.Intersect(
                            ReachCapabilities.WindowsHost,
                            capabilities.Capabilities)),
                    cancellationToken)
                .ConfigureAwait(false);

            var open = await ReadEnvelopeAsync(stream, cancellationToken)
                .ConfigureAwait(false);
            if (open?.Type is not ReachMessageType.SessionOpen
                and not ReachMessageType.SessionResume)
            {
                throw new InvalidDataException("The client did not open a session.");
            }

            await SendAsync(
                    stream,
                    ReachMessageType.VideoStreamStart,
                    new ReachVideoStreamStart("H264", 2, 2, 1),
                    cancellationToken)
                .ConfigureAwait(false);
            await SendAsync(
                    stream,
                    ReachMessageType.VideoFrame,
                    new ReachVideoFrame(
                        1,
                        2,
                        2,
                        DateTime.UtcNow.Ticks,
                        "H264",
                        true,
                        [1, 2, 3]),
                    cancellationToken)
                .ConfigureAwait(false);

            while (true)
            {
                var envelope = await ReadEnvelopeAsync(stream, cancellationToken)
                    .ConfigureAwait(false);
                if (envelope is null)
                    return;
                if (envelope.Type == ReachMessageType.PointerMove)
                {
                    inputReceived.TrySetResult(
                        ReachMessageCodec.ReadBody<ReachPointerMove>(envelope));
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task<T> ReadBodyAsync<T>(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var envelope = await ReadEnvelopeAsync(stream, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EndOfStreamException();
        return ReachMessageCodec.ReadBody<T>(envelope);
    }

    private static async Task<ReachMessageEnvelope?> ReadEnvelopeAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var frame = await LengthPrefixedFrameCodec.ReadAsync(stream, cancellationToken)
            .ConfigureAwait(false);
        return frame is null ? null : ReachMessageCodec.Deserialize(frame.Payload);
    }

    private static ValueTask SendAsync<T>(
        Stream stream,
        ReachMessageType type,
        T message,
        CancellationToken cancellationToken) =>
        LengthPrefixedFrameCodec.WriteAsync(
            stream,
            ReachMessageCodec.Serialize(type, DateTime.UtcNow.Ticks, message),
            cancellationToken);
}
