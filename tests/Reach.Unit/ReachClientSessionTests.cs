using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using Novolis.Reach.Client;
using Novolis.Reach.Protocol;
using ClientSessionState = Novolis.Reach.Client.ReachClientConnectionState;

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
        var statuses = new ConcurrentQueue<string>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var emulator = ReachClientSessionEmulator.RunAsync(
            listener,
            inputReceived,
            cancellation.Token);

        await using var session = new ReachClientSession(mediaPort: 0);
        session.StatusChanged += statuses.Enqueue;
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
        await Assert.That(session.State).IsEqualTo(ClientSessionState.Streaming);
        await Assert.That(statuses.Any(status =>
            status.Contains(
                "Connected to Reach.Unit Emulator",
                StringComparison.Ordinal))).IsTrue();

        await session.SendPointerMoveAsync(42, 24, cancellation.Token);
        var input = await inputReceived.Task.WaitAsync(cancellation.Token);
        await Assert.That(input.X).IsEqualTo(42);
        await Assert.That(input.Y).IsEqualTo(24);

        await session.DisconnectAsync().ConfigureAwait(false);
        await emulator.WaitAsync(cancellation.Token).ConfigureAwait(false);
    }

    [Test]
    public async Task ClientSessionReportsControlLossAndClearsConnectionState()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var connectionLost = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var emulator = ReachClientSessionEmulator.RunThenCloseAsync(
            listener,
            sendSessionClose: false,
            cancellation.Token);

        await using var session = new ReachClientSession(mediaPort: 0);
        session.ConnectionLost += () => connectionLost.TrySetResult(true);
        await session.ConnectAsync(
                $"tcp://127.0.0.1:{endpoint.Port}",
                ReachPlatform.Android,
                "Reach.Unit",
                cancellation.Token)
            .ConfigureAwait(false);

        await connectionLost.Task.WaitAsync(cancellation.Token);
        await Assert.That(session.IsConnected).IsFalse();
        await Assert.That(session.State).IsEqualTo(ClientSessionState.Lost);
        await emulator.WaitAsync(cancellation.Token).ConfigureAwait(false);
    }

    [Test]
    public async Task ClientSessionReportsRemoteSessionClose()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var sessionEnded = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var connectionLost = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var emulator = ReachClientSessionEmulator.RunThenCloseAsync(
            listener,
            sendSessionClose: true,
            cancellation.Token);

        await using var session = new ReachClientSession(mediaPort: 0);
        session.SessionEnded += reason => sessionEnded.TrySetResult(reason);
        session.ConnectionLost += () => connectionLost.TrySetResult(true);
        await session.ConnectAsync(
                $"tcp://127.0.0.1:{endpoint.Port}",
                ReachPlatform.Android,
                "Reach.Unit",
                cancellation.Token)
            .ConfigureAwait(false);

        var reason = await sessionEnded.Task.WaitAsync(cancellation.Token);
        await connectionLost.Task.WaitAsync(cancellation.Token);
        await Assert.That(reason).IsEqualTo("Test host closed the session.");
        await Assert.That(session.State).IsEqualTo(ClientSessionState.Lost);
        await emulator.WaitAsync(cancellation.Token).ConfigureAwait(false);
    }

    [Test]
    public async Task ClientSessionReportsMediaChannelLoss()
    {
        using var controlListener = new TcpListener(IPAddress.Loopback, 0);
        controlListener.Start();
        var controlPort = ((IPEndPoint)controlListener.LocalEndpoint).Port;
        using var mediaListener = new TcpListener(IPAddress.Loopback, 0);
        mediaListener.Start();
        var mediaPort = ((IPEndPoint)mediaListener.LocalEndpoint).Port;
        var mediaLost = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var emulator = ReachClientSessionEmulator.RunMediaAsync(
            controlListener,
            mediaListener,
            cancellation.Token);

        await using var session = new ReachClientSession(mediaPort);
        session.MediaConnectionLost += () => mediaLost.TrySetResult(true);
        await session.ConnectAsync(
                $"tcp://127.0.0.1:{controlPort}",
                ReachPlatform.Android,
                "Reach.Unit",
                cancellation.Token)
            .ConfigureAwait(false);

        await mediaLost.Task.WaitAsync(cancellation.Token);
        await Assert.That(session.IsConnected).IsTrue();
        await Assert.That(session.IsMediaConnected).IsFalse();
        await Assert.That(session.State).IsEqualTo(ClientSessionState.Connected);
        await session.DisconnectAsync().ConfigureAwait(false);
        cancellation.Cancel();
        await emulator.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task ClientSessionReconnectsAfterControlLoss()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var connectionLost = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var reconnected = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var emulator = ReachClientSessionEmulator.RunReconnectAsync(
            listener,
            reconnected,
            cancellation.Token);

        await using var session = new ReachClientSession(mediaPort: 0);
        session.ConnectionLost += () => connectionLost.TrySetResult(true);
        await session.ConnectAsync(
                $"tcp://127.0.0.1:{endpoint.Port}",
                ReachPlatform.Android,
                "Reach.Unit",
                cancellation.Token)
            .ConfigureAwait(false);

        await connectionLost.Task.WaitAsync(cancellation.Token);
        await session.ReconnectAsync(cancellation.Token);
        await reconnected.Task.WaitAsync(cancellation.Token);
        await Assert.That(session.IsConnected).IsTrue();
        await Assert.That(session.State).IsEqualTo(ClientSessionState.Connected);

        await session.DisconnectAsync().ConfigureAwait(false);
        cancellation.Cancel();
        await emulator.WaitAsync(TimeSpan.FromSeconds(2));
    }
}
