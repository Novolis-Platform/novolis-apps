using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Novolis.Reach.Protocol;
using Novolis.Transports.Discovery;
using Novolis.Transports.Framing;
using Novolis.Video;
using Novolis.Video.Codecs.H264;

namespace Novolis.Reach.Host.Windows.Emulator;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var port = ReadIntOption(args, "--port", ReachProtocol.ControlPort);
        var framesPerSecond = ReadIntOption(args, "--fps", 12);
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        await using var emulator = new ReachLocalEmulator(port, framesPerSecond);
        Console.WriteLine(
            $"Reach local emulator listening on tcp://127.0.0.1:{port}. "
            + "Press Ctrl+C to stop.");
        await emulator.RunAsync(cancellation.Token).ConfigureAwait(false);
    }

    private static int ReadIntOption(
        string[] args,
        string name,
        int fallback)
    {
        var index = Array.IndexOf(args, name);
        if (index < 0 || index + 1 >= args.Length)
            return fallback;

        return int.TryParse(args[index + 1], out var value) && value > 0
            ? value
            : fallback;
    }
}

/// <summary>
/// Generates a deterministic H.264 stream and records client input without
/// touching the interactive Windows session.
/// </summary>
public sealed class ReachLocalEmulator : IAsyncDisposable
{
    private const int Width = 960;
    private const int Height = 540;
    private const int MaxClients = 8;
    private readonly int _port;
    private readonly int _framesPerSecond;
    private readonly TcpListener _listener;
    private readonly ConcurrentDictionary<long, Task> _clients = new();
    private long _clientSequence;
    private bool _disposed;

    /// <summary>Creates a local-only Reach protocol emulator.</summary>
    public ReachLocalEmulator(int port = ReachProtocol.ControlPort, int framesPerSecond = 12)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(port, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        ArgumentOutOfRangeException.ThrowIfLessThan(framesPerSecond, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(framesPerSecond, 60);
        _port = port;
        _framesPerSecond = framesPerSecond;
        _listener = new TcpListener(IPAddress.Loopback, port);
    }

    /// <summary>Runs the emulator until cancellation.</summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _listener.Start(MaxClients);
        await using var discovery = new DiscoveryResponder(
            new IPEndPoint(IPAddress.Loopback, ReachProtocol.DiscoveryPort),
            ReachProtocol.DiscoveryProbe,
            new DiscoveryBeacon(
                ReachProtocol.AppId,
                ReachProtocol.Version,
                "Reach Local Emulator",
                [$"tcp://127.0.0.1:{_port}"]));
        var discoveryTask = discovery.RunAsync(cancellationToken);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken)
                    .ConfigureAwait(false);
                client.NoDelay = true;
                var id = Interlocked.Increment(ref _clientSequence);
                var task = HandleClientAsync(id, client, cancellationToken);
                _clients[id] = task;
                _ = task.ContinueWith(
                    completedTask =>
                    {
                        _clients.TryRemove(id, out var ignored);
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            _listener.Stop();
            foreach (var task in _clients.Values)
            {
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"Reach emulator client ended: {exception.Message}");
                }
            }

            try
            {
                await discoveryTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _disposed = true;
        _listener.Stop();
        return ValueTask.CompletedTask;
    }

    private async Task HandleClientAsync(
        long id,
        TcpClient client,
        CancellationToken cancellationToken)
    {
        using var clientLifetime = client;
        var stream = client.GetStream();
        var sendGate = new SemaphoreSlim(1, 1);
        using var sessionCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            var hello = await ReadMessageAsync<ReachClientHello>(
                    stream,
                    sessionCancellation.Token)
                .ConfigureAwait(false);
            if (!string.Equals(hello.AppId, ReachProtocol.AppId, StringComparison.Ordinal)
                || !ReachProtocol.IsCompatible(hello.ProtocolVersion))
            {
                throw new InvalidDataException("Incompatible Reach client hello.");
            }

            var offered = await ReadMessageAsync<ReachCapabilitiesMessage>(
                    stream,
                    sessionCancellation.Token)
                .ConfigureAwait(false);
            var negotiated = ReachCapabilities.Intersect(
                ReachCapabilities.WindowsHost,
                offered.Capabilities);
            await SendAsync(
                    stream,
                    sendGate,
                    ReachMessageType.HostHello,
                    new ReachHostHello(
                        ReachProtocol.AppId,
                        ReachProtocol.Version,
                        "Reach Local Emulator",
                        [$"tcp://127.0.0.1:{_port}"]),
                    sessionCancellation.Token)
                .ConfigureAwait(false);
            await SendAsync(
                    stream,
                    sendGate,
                    ReachMessageType.HostCapabilities,
                    new ReachCapabilitiesMessage(negotiated),
                    sessionCancellation.Token)
                .ConfigureAwait(false);

            var open = await ReadEnvelopeAsync(stream, sessionCancellation.Token)
                .ConfigureAwait(false)
                ?? throw new EndOfStreamException("Client closed during session open.");
            if (open.Type is not ReachMessageType.SessionOpen
                and not ReachMessageType.SessionResume)
            {
                throw new InvalidDataException(
                    $"Expected a Reach session open, received {open.Type}.");
            }

            Console.WriteLine(
                $"Reach emulator client {id} connected from {client.Client.RemoteEndPoint} "
                + $"({hello.Platform}, {hello.ClientName}).");
            await SendAsync(
                    stream,
                    sendGate,
                    ReachMessageType.VideoStreamStart,
                    new ReachVideoStreamStart(
                        "H264",
                        Width,
                        Height,
                        _framesPerSecond),
                    sessionCancellation.Token)
                .ConfigureAwait(false);
            await SendAsync(
                    stream,
                    sendGate,
                    ReachMessageType.DisplayTopology,
                    new ReachDisplayTopology(
                    [
                        new ReachDisplay(
                            "emulator-display",
                            0,
                            0,
                            Width,
                            Height,
                            96),
                    ]),
                    sessionCancellation.Token)
                .ConfigureAwait(false);

            var receiveTask = ReceiveClientAsync(
                id,
                stream,
                sessionCancellation.Token);
            var videoTask = SendVideoAsync(
                stream,
                sendGate,
                sessionCancellation.Token);
            await Task.WhenAny(receiveTask, videoTask).ConfigureAwait(false);
            sessionCancellation.Cancel();
            await Task.WhenAll(receiveTask, videoTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (sessionCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Reach emulator client {id} failed: {exception.Message}");
        }
        finally
        {
            sendGate.Dispose();
            Console.WriteLine($"Reach emulator client {id} disconnected.");
        }
    }

    private async Task ReceiveClientAsync(
        long id,
        Stream stream,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var envelope = await ReadEnvelopeAsync(stream, cancellationToken)
                .ConfigureAwait(false);
            if (envelope is null)
                return;

            switch (envelope.Type)
            {
                case ReachMessageType.SessionClose:
                    return;
                case ReachMessageType.PointerMove:
                {
                    var move = ReachMessageCodec.ReadBody<ReachPointerMove>(envelope);
                    Console.WriteLine(
                        $"Reach emulator client {id}: pointer {move.X:0},{move.Y:0}");
                    break;
                }
                case ReachMessageType.PointerButton:
                {
                    var button = ReachMessageCodec.ReadBody<ReachPointerButton>(envelope);
                    Console.WriteLine(
                        $"Reach emulator client {id}: {button.Button} "
                        + (button.IsDown ? "down" : "up"));
                    break;
                }
                case ReachMessageType.KeyDown:
                case ReachMessageType.KeyUp:
                {
                    var key = ReachMessageCodec.ReadBody<ReachKeyEvent>(envelope);
                    Console.WriteLine(
                        $"Reach emulator client {id}: key {key.VirtualKey} "
                        + (envelope.Type == ReachMessageType.KeyDown ? "down" : "up"));
                    break;
                }
                case ReachMessageType.TextInput:
                {
                    var text = ReachMessageCodec.ReadBody<ReachTextInput>(envelope);
                    Console.WriteLine($"Reach emulator client {id}: text {text.Text}");
                    break;
                }
                case ReachMessageType.RequestKeyFrame:
                    Console.WriteLine($"Reach emulator client {id}: key frame requested.");
                    break;
                case ReachMessageType.ClipboardContent:
                    Console.WriteLine($"Reach emulator client {id}: clipboard update received.");
                    break;
            }
        }
    }

    private async Task SendVideoAsync(
        Stream stream,
        SemaphoreSlim sendGate,
        CancellationToken cancellationToken)
    {
        using var encoder = new WindowsH264Encoder(
            Width,
            Height,
            _framesPerSecond,
            averageBitrate: 2_000_000);
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(1d / _framesPerSecond));
        var frameNumber = 0L;
        var emittedFrames = 0;

        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            var raw = CreateFrame(frameNumber++);
            try
            {
                var encoded = encoder.Encode(raw);
                await SendAsync(
                        stream,
                        sendGate,
                        ReachMessageType.VideoFrame,
                        new ReachVideoFrame(
                            frameNumber,
                            encoded.Width,
                            encoded.Height,
                            encoded.Timestamp,
                            encoded.Codec,
                            encoded.IsKeyFrame,
                            encoded.AccessUnit),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (Interlocked.Increment(ref emittedFrames) == 1)
                {
                    Console.WriteLine(
                        $"Reach emulator video started: {encoded.Width}x{encoded.Height}, "
                        + $"{encoded.AccessUnit.Length} bytes.");
                }
            }
            catch (InvalidOperationException exception)
            {
                // Media Foundation may need one input cycle before its first
                // access unit is available.
                if (frameNumber <= 3)
                    Console.WriteLine($"Reach emulator video encoder priming: {exception.Message}");
            }
        }
    }

    private static RawVideoFrame CreateFrame(long frameNumber)
    {
        var stride = Width * 4;
        var pixels = new byte[stride * Height];
        var phase = (int)(frameNumber % 240);
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var offset = (y * stride) + (x * 4);
                pixels[offset] = (byte)((x + phase) % 256);
                pixels[offset + 1] = (byte)((y * 255 / Height + phase) % 256);
                pixels[offset + 2] = (byte)((x + y + phase) % 256);
                pixels[offset + 3] = byte.MaxValue;
            }
        }

        return new RawVideoFrame(
            Width,
            Height,
            stride,
            VideoPixelFormat.Bgra32,
            pixels,
            DateTime.UtcNow.Ticks);
    }

    private static async Task SendAsync<T>(
        Stream stream,
        SemaphoreSlim sendGate,
        ReachMessageType type,
        T message,
        CancellationToken cancellationToken)
    {
        await sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var payload = ReachMessageCodec.Serialize(
                type,
                DateTime.UtcNow.Ticks,
                message);
            await LengthPrefixedFrameCodec.WriteAsync(
                    stream,
                    payload,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            sendGate.Release();
        }
    }

    private static async Task<T> ReadMessageAsync<T>(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var envelope = await ReadEnvelopeAsync(stream, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EndOfStreamException("Reach client closed the control stream.");
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
}
