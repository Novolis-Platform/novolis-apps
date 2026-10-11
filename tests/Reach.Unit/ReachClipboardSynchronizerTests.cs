using System.Net;
using System.Net.Sockets;
using Novolis.Reach.Client;
using Novolis.Reach.Protocol;

namespace Reach.Unit;

public sealed class ReachClipboardSynchronizerTests
{
    [Test]
    public async Task RemoteTextIsAppliedAndLocalChangesAreSentOnce()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var received = new TaskCompletionSource<ReachClipboardContent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var emulator = ReachClientSessionEmulator.RunClipboardAsync(
            listener,
            received,
            cancellation.Token);
        var bridge = new FakeClipboardBridge();
        await using var session = new ReachClientSession(mediaPort: 0);
        await using var synchronizer = new ReachClipboardSynchronizer(session, bridge);

        await synchronizer.StartAsync(cancellation.Token);
        await session.ConnectAsync(
                $"tcp://127.0.0.1:{endpoint.Port}",
                ReachPlatform.Windows,
                "Reach.Unit",
                cancellation.Token)
            .ConfigureAwait(false);
        await synchronizer.RequestRemoteClipboardAsync(cancellation.Token);

        await WaitForAsync(
                () => bridge.Text == "from-host",
                cancellation.Token)
            .ConfigureAwait(false);
        await Assert.That(bridge.WriteCount).IsEqualTo(1);

        bridge.SetLocalText("from-client");
        var content = await received.Task.WaitAsync(cancellation.Token);
        await Assert.That(content.Format).IsEqualTo("text");
        await Assert.That(content.Text).IsEqualTo("from-client");

        bridge.RaiseChanged();
        await Task.Delay(100, cancellation.Token).ConfigureAwait(false);
        await Assert.That(bridge.ReadCount).IsGreaterThanOrEqualTo(1);

        await session.DisconnectAsync().ConfigureAwait(false);
        cancellation.Cancel();
        await emulator.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
    }

    [Test]
    public async Task DisposalStopsAndDisposesTheInjectedBridge()
    {
        var bridge = new FakeClipboardBridge();
        await using var session = new ReachClientSession(mediaPort: 0);
        var synchronizer = new ReachClipboardSynchronizer(session, bridge);

        await synchronizer.StartAsync().ConfigureAwait(false);
        await synchronizer.DisposeAsync().ConfigureAwait(false);

        await Assert.That(bridge.StopCount).IsEqualTo(1);
        await Assert.That(bridge.DisposeCount).IsEqualTo(1);
    }

    private static async Task WaitForAsync(
        Func<bool> predicate,
        CancellationToken cancellationToken)
    {
        while (!predicate())
        {
            await Task.Delay(25, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class FakeClipboardBridge : IReachClipboardBridge
    {
        public string? Text { get; private set; }

        public int ReadCount { get; private set; }

        public int WriteCount { get; private set; }

        public int StopCount { get; private set; }

        public int DisposeCount { get; private set; }

        public event Action? Changed;

        public Task StartAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string?> ReadTextAsync(
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return Task.FromResult(Text);
        }

        public Task WriteTextAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            Text = text;
            WriteCount++;
            Changed?.Invoke();
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            StopCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }

        internal void SetLocalText(string text)
        {
            Text = text;
            Changed?.Invoke();
        }

        internal void RaiseChanged() => Changed?.Invoke();
    }
}
