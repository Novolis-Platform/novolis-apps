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

internal sealed class ReachClientViewFrames(ReachClientView view)
{
        internal void OnVideoStreamReset(ReachVideoStreamReset reset)
        {
            ResetVideoStreamForRecovery();
            view.OnStatusChanged($"Remote video stream reset at frame {reset.Sequence}.");
        }

        internal void ResetVideoStreamForRecovery()
        {
            void Reset()
            {
                lock (view._frameGate)
                {
                    view._pendingFrame = null;
                    view._frameUpdateScheduled = false;
                }

                view._streamStatusShown = false;
                view._touchPoints.Clear();
                view._touchGestureActive = false;
                view._touchRemoteButtonDown = false;
                view.CancelTouchLongPress();
                view._touchLongPressFired = false;
                view._videoZoom = 1;
                view._videoScale.ScaleX = 1;
                view._videoScale.ScaleY = 1;
                view._videoTranslation.X = 0;
                view._videoTranslation.Y = 0;
                if (view._presenter is IReachVideoStreamResetter streamResetter)
                    streamResetter.ResetStream();
            }

            if (Dispatcher.UIThread.CheckAccess())
                Reset();
            else
                Dispatcher.UIThread.Post(Reset);
        }

        internal void UpdateConnectionControls()
        {
            void Apply()
            {
                if (view._session.State == ReachClientConnectionState.Connecting)
                {
                    view._connect.Content = "Cancel";
                    view._connect.IsEnabled = true;
                    return;
                }

                view._connect.IsEnabled = !view._discoveryActive
                    && !string.IsNullOrWhiteSpace(view._endpoint.Text)
                    && view._session.State is not ReachClientConnectionState.Connecting;
            }

            if (Dispatcher.UIThread.CheckAccess())
                Apply();
            else
                Dispatcher.UIThread.Post(Apply);
        }

        internal void OnFrameDecoded(RawVideoFrame frame)
        {
            if (frame.Format != VideoPixelFormat.Bgra32
                || !view._session.IsConnected)
                return;

            view._videoWidth = frame.Width;
            view._videoHeight = frame.Height;
            if (!view._streamStatusShown)
            {
                view._streamStatusShown = true;
                view.OnStatusChanged(
                    $"Streaming remote session ({frame.Width}x{frame.Height}).");
            }
            lock (view._frameGate)
            {
                view._pendingFrame = frame;
                if (view._frameUpdateScheduled)
                    return;

                view._frameUpdateScheduled = true;
            }

            Dispatcher.UIThread.Post(
                ApplyPendingFrame,
                DispatcherPriority.Render);
        }

        internal void ApplyPendingFrame()
        {
            RawVideoFrame? frame;
            lock (view._frameGate)
            {
                frame = view._pendingFrame;
                view._pendingFrame = null;
            }

            if (frame is not null)
            {
                ApplyFrame(frame);
            }

            lock (view._frameGate)
            {
                if (view._pendingFrame is null)
                {
                    view._frameUpdateScheduled = false;
                    return;
                }
            }

            Dispatcher.UIThread.Post(
                ApplyPendingFrame,
                DispatcherPriority.Render);
        }

        internal void ApplyFrame(RawVideoFrame frame)
        {
            var start = Stopwatch.GetTimestamp();
            view._videoImage.Present(frame);
            view._session.RecordPresentedFrame(
                frame.Timestamp,
                Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }

        internal void ClearVideoFrame()
        {
            void Clear()
            {
                view.Keyboard.ReleasePressedKeys();
                lock (view._frameGate)
                {
                    view._pendingFrame = null;
                    view._frameUpdateScheduled = false;
                }
                view._videoImage.Clear();
                view._videoWidth = 0;
                view._videoHeight = 0;
                view._platformVideoConfigured = false;
                view._videoProfileController = null;
                view._activeVideoProfile = null;
                view._streamStatusShown = false;
                view._touchPoints.Clear();
                view._touchGestureActive = false;
                view._touchRemoteButtonDown = false;
                view.CancelTouchLongPress();
                view._touchLongPressFired = false;
                view._videoZoom = 1;
                view._videoScale.ScaleX = 1;
                view._videoScale.ScaleY = 1;
                view._videoTranslation.X = 0;
                view._videoTranslation.Y = 0;
                if (view._presenter is IReachVideoStreamResetter streamResetter)
                    streamResetter.ResetStream();
            }

            if (Dispatcher.UIThread.CheckAccess())
                Clear();
            else
                Dispatcher.UIThread.Post(Clear);
        }

        internal void OnKeyFrameRequested() =>
            view.QueueInput(() => view._session.RequestKeyFrameAsync());

        internal void OnDisplayTopology(ReachDisplayTopology topology)
        {
            var displays = topology.Displays
                .Where(static display =>
                    display.Width > 0
                    && display.Height > 0)
                .ToArray();
            var display = displays.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.Id,
                    view._selectedDisplayId,
                    StringComparison.Ordinal))
                ?? displays.FirstOrDefault();
            if (display is null)
                return;

            void Apply()
            {
                view._displays = displays;
                view._selectedDisplayId = display.Id;
                view._displaySelectionUpdating = true;
                try
                {
                    view._displaySelector.ItemsSource = displays
                        .Select((candidate, index) =>
                            FormatDisplay(candidate, index))
                        .ToArray();
                    view._displaySelector.SelectedIndex = Array.FindIndex(
                        displays,
                        candidate => string.Equals(
                            candidate.Id,
                            display.Id,
                            StringComparison.Ordinal));
                }
                finally
                {
                    view._displaySelectionUpdating = false;
                }

                view._displaySelector.IsVisible =
                    OperatingSystem.IsWindows()
                    && displays.Length > 1;
                ApplyDisplayBounds(view, display);
            }

            if (Dispatcher.UIThread.CheckAccess())
                Apply();
            else
                Dispatcher.UIThread.Post(Apply);

            if ((OperatingSystem.IsAndroid() || OperatingSystem.IsWindows())
                && !view._platformVideoConfigured)
            {
                view._platformVideoConfigured = true;
                view._videoProfileController = new ReachVideoProfileController(
                    display,
                    ResolveInitialVideoProfileKind(
                        Volatile.Read(ref view._endpointValue)));
                var profile = view._videoProfileController.Current;
                view._activeVideoProfile = profile;
                view.QueueInput(() => view._session.ConfigureVideoAsync(
                    profile.Width,
                    profile.Height,
                    profile.FramesPerSecond,
                    profile.TargetBitrate));
            }
        }

        internal void DisplaySelectionChanged(
            object? sender,
            SelectionChangedEventArgs args)
        {
            if (view._displaySelectionUpdating
                || view._displaySelector.SelectedIndex < 0
                || view._displaySelector.SelectedIndex >= view._displays.Count)
            {
                return;
            }

            var display = view._displays[view._displaySelector.SelectedIndex];
            if (string.Equals(
                    display.Id,
                    view._selectedDisplayId,
                    StringComparison.Ordinal))
            {
                return;
            }

            view._selectedDisplayId = display.Id;
            view._platformVideoConfigured = false;
            ApplyDisplayBounds(view, display);
            view.OnStatusChanged(
                $"Switching to display {view._displaySelector.SelectedIndex + 1}.");
            view.QueueInput(
                () => view._session.SelectDisplayAsync(display.Id));
        }

        private static void ApplyDisplayBounds(
            ReachClientView view,
            ReachDisplay display)
        {
            view._selectedDisplayLeft = display.Left;
            view._selectedDisplayTop = display.Top;
            view._selectedDisplayWidth = display.Width;
            view._selectedDisplayHeight = display.Height;
        }

        private static string FormatDisplay(
            ReachDisplay display,
            int index) =>
            $"{index + 1}: {display.Width}x{display.Height}"
            + $" at {display.Left},{display.Top}";

        internal void OnVideoStreamStarted(ReachVideoStreamStart stream)
        {
            if (stream.Width > 0)
                view._videoWidth = stream.Width;
            if (stream.Height > 0)
                view._videoHeight = stream.Height;
        }

        internal static ReachVideoProfileKind ResolveInitialVideoProfileKind(
            string? endpoint) =>
            endpoint?.Contains("100.", StringComparison.Ordinal) == true
                || endpoint?.Contains(".ts.net", StringComparison.OrdinalIgnoreCase)
                    == true
                ? ReachVideoProfileKind.Routed
                : ReachVideoProfileKind.Lan;
}

