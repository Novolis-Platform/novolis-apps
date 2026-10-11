using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Novolis.Avalonia.Video;
using Novolis.Reach.Client;
using Novolis.Video;

namespace Novolis.Avalonia.Reach;

internal sealed class ReachClientViewStatus(ReachClientView view)
{
        internal void OnStatusChanged(string status)
        {
            var priority = GetStatusPriority(status);
            void Apply()
            {
                if (priority < view._statusPriority)
                    return;

                view._statusPriority = priority;
                view._status.Text = status;
            }

            if (Dispatcher.UIThread.CheckAccess())
                Apply();
            else
                Dispatcher.UIThread.Post(Apply);
        }

        internal void ResetStatusPriority()
        {
            if (Dispatcher.UIThread.CheckAccess())
                view._statusPriority = 0;
            else
                Dispatcher.UIThread.Post(() => view._statusPriority = 0);
        }

        internal void OnPhaseChanged(ReachConnectionPhase phase)
        {
            void Apply() => view._sessionPhase.Text = $"Phase: {phase}";
            if (Dispatcher.UIThread.CheckAccess())
                Apply();
            else
                Dispatcher.UIThread.Post(Apply);
        }

        internal void OnPerformanceChanged(ReachPerformanceSnapshot snapshot)
        {
            void Apply()
            {
                var frameAge = snapshot.FrameAgeP95Milliseconds is { } age
                    ? $"{age:0} ms"
                    : "n/a";
                var roundTrip = snapshot.InputRoundTripP95Milliseconds is { } rtt
                    ? $"{rtt:0} ms"
                    : "n/a";
                var profile = view._activeVideoProfile is { } activeProfile
                    ? $"quality: {activeProfile.Kind} "
                      + $"({activeProfile.Width}x{activeProfile.Height}, "
                      + $"{activeProfile.FramesPerSecond} FPS); "
                    : string.Empty;
                view._performanceStatus.Text =
                    $"Transport: {view._session.ActiveTransport}; "
                    + profile
                    + $"frame age p95: {frameAge}; input RTT p95: {roundTrip}; "
                    + $"drops: {snapshot.DroppedFrames}; "
                    + $"keyframes: {snapshot.KeyFrameRequests}";
                view._performanceStatus.IsVisible =
                    (OperatingSystem.IsAndroid() || OperatingSystem.IsWindows())
                    && snapshot.ReceivedFrames > 0;
            }

            if (view._videoProfileController is { } profileController
                && view._session.IsConnected
                && view._session.NegotiatedCapabilities?.Supports(
                    ReachCapability.AdaptiveQuality) == true
                && profileController.Observe(
                        snapshot,
                        DateTimeOffset.UtcNow)
                    is { } profile)
            {
                view._activeVideoProfile = profile;
                view.QueueInput(() => view._session.ConfigureVideoAsync(
                    profile.Width,
                    profile.Height,
                    profile.FramesPerSecond,
                    profile.TargetBitrate));
                view.OnStatusChanged(
                    $"Video quality adjusted to {profile.Kind} "
                    + $"({profile.FramesPerSecond} FPS).");
            }

            if (Dispatcher.UIThread.CheckAccess())
                Apply();
            else
                Dispatcher.UIThread.Post(Apply);
        }

        internal void SetConnectedStatus(bool reconnected = false)
        {
            if (view._session.State == ReachClientConnectionState.Streaming
                && view._session.LastVideoFrameAt is not null)
            {
                var dimensions = view._videoWidth > 0 && view._videoHeight > 0
                    ? $" ({view._videoWidth}x{view._videoHeight})"
                    : string.Empty;
                view.OnStatusChanged($"Streaming remote session{dimensions}.");
                return;
            }

            view.OnStatusChanged(
                reconnected
                    ? "Reconnected; waiting for the remote session stream..."
                    : "Connected; waiting for the remote session stream...");
        }

        internal static int GetStatusPriority(string status) =>
            status.StartsWith("Streaming", StringComparison.OrdinalIgnoreCase)
                ? 3
                : status.StartsWith("Remote video stream reset", StringComparison.OrdinalIgnoreCase)
                    || status.StartsWith("Connected", StringComparison.OrdinalIgnoreCase)
                    || status.StartsWith("Reconnected", StringComparison.OrdinalIgnoreCase)
                    || status.StartsWith("Waiting", StringComparison.OrdinalIgnoreCase)
                        ? 2
                        : status.StartsWith("Remote session ended", StringComparison.OrdinalIgnoreCase)
                            || status.StartsWith("Remote video stream lost", StringComparison.OrdinalIgnoreCase)
                            || status.StartsWith("Reach connection lost", StringComparison.OrdinalIgnoreCase)
                            || status.StartsWith("Connection failed", StringComparison.OrdinalIgnoreCase)
                            || status.StartsWith("Reconnect failed", StringComparison.OrdinalIgnoreCase)
                                ? 4
                                : 1;

        internal void OnSessionStateChanged(ReachClientConnectionState state)
        {
            void Apply()
            {
                view._connect.Content = state is ReachClientConnectionState.Lost
                    or ReachClientConnectionState.Connected
                    or ReachClientConnectionState.Streaming
                    ? "Reconnect"
                    : "Connect";
                view.UpdateConnectionControls();
            }

            if (Dispatcher.UIThread.CheckAccess())
                Apply();
            else
                Dispatcher.UIThread.Post(Apply);
        }

        internal void OnSessionEnded(string reason)
        {
            view.ReleasePressedKeys();
            view._sessionEnded = true;
            view.ClearVideoFrame();
            view.OnStatusChanged($"Remote session ended: {reason}");
        }

        internal void OnSharingStateChanged(ReachSharingState state)
        {
            view.OnStatusChanged(
                state.IsPaused
                    ? $"Host paused sharing: {state.Reason}"
                    : "Host resumed sharing.");
        }

        internal void OnMediaConnectionLost()
        {
            view.ClearVideoFrame();
            view.OnStatusChanged("Video is recovering; input remains available.");
            _ = view.RecoverMediaAsync();
            view.UpdateConnectionControls();
        }

        internal void OnConnectionLost()
        {
            view.ReleasePressedKeys();
            view.ClearVideoFrame();
            if (view._sessionEnded)
            {
                view.OnStatusChanged(
                    "The remote session ended. Reconnect when the host is ready.");
                view.UpdateConnectionControls();
                return;
            }

            view.OnStatusChanged("Connection lost. Reconnecting...");
            view.BeginReconnectLoop();
            view.UpdateConnectionControls();
        }

        internal async Task RecoverMediaAsync()
        {
            try
            {
                using var timeout = new CancellationTokenSource(
                    TimeSpan.FromSeconds(5));
                await view._session.RecoverMediaAsync(timeout.Token);
                view.OnStatusChanged("Video recovered; waiting for a fresh frame.");
            }
            catch (Exception exception)
            {
                view.OnStatusChanged($"Video recovery failed: {exception.Message}");
            }
        }

        internal void BeginReconnectLoop()
        {
            if (view._reconnectCancellation is not null
                || view._session.State is not ReachClientConnectionState.Lost)
            {
                return;
            }

            var cancellation = new CancellationTokenSource();
            view._reconnectCancellation = cancellation;
            _ = ReconnectLoopAsync(cancellation);
        }

        internal async Task ReconnectLoopAsync(CancellationTokenSource owner)
        {
            try
            {
                for (var attempt = 1; attempt <= 3; attempt++)
                {
                    var delay = TimeSpan.FromSeconds(attempt);
                    view.OnStatusChanged(
                        $"Reconnecting in {delay.TotalSeconds:0}s "
                        + $"(attempt {attempt} of 3)...");
                    await Task.Delay(delay, owner.Token);
                    if (await view.ReconnectToEndpointAsync(
                            owner.Token,
                            cancelExistingReconnect: false))
                        return;
                }

                view.OnStatusChanged(
                    "Reach is offline. Check the host and tap Reconnect.");
            }
            catch (OperationCanceledException) when (owner.IsCancellationRequested)
            {
            }
            finally
            {
                if (ReferenceEquals(
                        Interlocked.CompareExchange(
                            ref view._reconnectCancellation,
                            null,
                            owner),
                        owner))
                {
                    owner.Dispose();
                }
            }
        }

        internal void CancelReconnect()
        {
            var cancellation = Interlocked.Exchange(
                ref view._reconnectCancellation,
                null);
            cancellation?.Cancel();
            cancellation?.Dispose();
        }
}

