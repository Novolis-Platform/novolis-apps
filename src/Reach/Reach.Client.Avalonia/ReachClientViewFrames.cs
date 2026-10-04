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
            var display = topology.Displays.FirstOrDefault();
            if (display is null)
                return;

            void Apply()
            {
                view._selectedDisplayLeft = display.Left;
                view._selectedDisplayTop = display.Top;
                view._selectedDisplayWidth = display.Width;
                view._selectedDisplayHeight = display.Height;
            }

            if (Dispatcher.UIThread.CheckAccess())
                Apply();
            else
                Dispatcher.UIThread.Post(Apply);

            if ((OperatingSystem.IsAndroid() || OperatingSystem.IsWindows())
                && !view._platformVideoConfigured)
            {
                view._platformVideoConfigured = true;
                if (OperatingSystem.IsWindows())
                {
                    view.QueueInput(() => view._session.ConfigureVideoAsync(
                        display.Width,
                        display.Height,
                        30,
                        8_000_000));
                }
                else
                {
                    view._videoProfileController = new ReachVideoProfileController(
                        display,
                        ResolveInitialVideoProfileKind(
                            Volatile.Read(ref view._endpointValue)));
                    var profile = view._videoProfileController.Current;
                    view.QueueInput(() => view._session.ConfigureVideoAsync(
                        profile.Width,
                        profile.Height,
                        profile.FramesPerSecond,
                        profile.TargetBitrate));
                }
            }
        }

        internal void OnVideoStreamStarted(ReachVideoStreamStart stream)
        {
            if (stream.Width > 0)
                view._videoWidth = stream.Width;
            if (stream.Height > 0)
                view._videoHeight = stream.Height;
        }

        internal void OnClipboardContent(ReachClipboardContent content)
        {
            var description = content.Format switch
            {
                "text" when content.Text is { Length: > 0 } => "Remote clipboard text received.",
                "files" when content.Files is { Length: > 0 } =>
                    $"Remote clipboard: {content.Files.Length} file(s) received.",
                _ => "Remote clipboard content received.",
            };
            view.OnStatusChanged(description);
        }

        internal static ReachVideoProfileKind ResolveInitialVideoProfileKind(
            string? endpoint) =>
            endpoint?.Contains("100.", StringComparison.Ordinal) == true
                || endpoint?.Contains(".ts.net", StringComparison.OrdinalIgnoreCase)
                    == true
                ? ReachVideoProfileKind.Routed
                : ReachVideoProfileKind.Lan;
}

