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

internal sealed class ReachClientViewKeyboard(ReachClientView view)
{
        internal void SendTextClicked(
            object? sender,
            RoutedEventArgs args)
        {
            SendRemoteText();
            view._remoteTextInput.Focus();
        }

        internal void KeyboardToggleClicked(
            object? sender,
            RoutedEventArgs args)
        {
            view._keyboardMode = !view._keyboardMode;
            view._remoteTextInput.IsVisible = view._keyboardMode;
            view._sendText.IsVisible = view._keyboardMode;
            view._keyboardToggle.Content = view._keyboardMode
                ? "Hide keyboard"
                : "Keyboard";
            if (view._keyboardMode)
            {
                view._remoteTextInput.Focus();
                view._remoteTextInput.SelectAll();
                view.OnStatusChanged(
                    "Keyboard mode active. Type, compose, then press Send.");
            }
            else
            {
                view._videoSurface.Focus();
                view.OnStatusChanged("Remote surface focused.");
            }
        }

        internal void ScrollModeClicked(
            object? sender,
            RoutedEventArgs args)
        {
            view._scrollModeEnabled = !view._scrollModeEnabled;
            view._scrollMode.Content = view._scrollModeEnabled
                ? "Scroll: on"
                : "Scroll";
            view.OnStatusChanged(
                view._scrollModeEnabled
                    ? "Two-finger scrolling is active."
                    : "Two-finger pan and pinch are active.");
        }

        internal void RemoteTextKeyDown(object? sender, KeyEventArgs args)
        {
            if (args.Key != Key.Return)
                return;

            SendRemoteText();
            args.Handled = true;
        }

        internal void SendRemoteText()
        {
            var text = view._remoteTextInput.Text;
            if (string.IsNullOrEmpty(text))
                return;

            view.QueueInput(() => view._session.SendTextInputAsync(text));
            view._remoteTextInput.Clear();
        }

        internal void OnVideoKeyDown(object? sender, KeyEventArgs args)
        {
            var shortcutModifiers = KeyModifiers.Control | KeyModifiers.Alt;
            if (ReachClientKeyMap.IsPrintable(args.Key)
                && (args.KeyModifiers & shortcutModifiers) == KeyModifiers.None)
                return;
            if (!ReachClientKeyMap.TryGetVirtualKey(args.Key, out var virtualKey))
            {
                return;
            }

            if (!view._pressedKeys.TryPress(args.Key))
                return;

            view.QueueInput(() => view._session.SendKeyAsync(virtualKey, true));
            args.Handled = true;
        }

        internal void OnVideoKeyUp(object? sender, KeyEventArgs args)
        {
            if (!view._pressedKeys.TryRelease(args.Key))
                return;

            if (!ReachClientKeyMap.TryGetVirtualKey(args.Key, out var virtualKey))
            {
                return;
            }

            view.QueueInput(() => view._session.SendKeyAsync(virtualKey, false));
            args.Handled = true;
        }

        internal void OnVideoTextInput(object? sender, TextInputEventArgs args)
        {
            if (args.Text is not { Length: > 0 })
                return;

            view.QueueInput(() => view._session.SendTextInputAsync(args.Text));
            args.Handled = true;
        }

        internal void ReleasePressedKeys()
        {
            var pressed = view._pressedKeys.ReleaseAll();

            foreach (var key in pressed)
            {
                if (ReachClientKeyMap.TryGetVirtualKey(key, out var virtualKey))
                    view.QueueInput(
                        () => view._session.SendKeyAsync(virtualKey, false));
            }
        }

        internal bool TryGetRemotePoint(
            PointerEventArgs args,
            out double x,
            out double y)
        {
            return view.TryGetRemotePoint(
                args.GetPosition(view._videoSurface),
                out x,
                out y);
        }

        internal bool TryGetRemotePoint(
            Point point,
            out double x,
            out double y)
        {
            x = 0;
            y = 0;
            if (view._videoWidth <= 0 || view._videoHeight <= 0)
                return false;

            var bounds = view._videoSurface.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return false;

            var fit = ReachVideoGeometry.CalculateFit(
                bounds.Width,
                bounds.Height,
                view._videoWidth,
                view._videoHeight,
                view._videoZoom,
                view._videoTranslation.X,
                view._videoTranslation.Y);
            return ReachVideoGeometry.TryMapPoint(
                fit,
                point.X,
                point.Y,
                view._videoWidth,
                view._videoHeight,
                view._selectedDisplayLeft,
                view._selectedDisplayTop,
                view._selectedDisplayWidth,
                view._selectedDisplayHeight,
                out x,
                out y);
        }

        internal void QueueInput(Func<Task> input)
        {
            lock (view._inputGate)
            {
                QueueInputLocked(input);
            }
        }

        internal void QueueInputLocked(Func<Task> input)
        {
            view._inputTail = view._inputTail
                .ContinueWith(
                    _ => SendInputAsync(input),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default)
                .Unwrap();
        }

        internal void QueuePointerMove(double x, double y)
        {
            lock (view._inputGate)
            {
                view._pendingPointerMove = new Point(x, y);
                if (view._pointerMoveQueued)
                    return;

                view._pointerMoveQueued = true;
                QueueInputLocked(SendLatestPointerMoveAsync);
            }
        }

        internal async Task SendLatestPointerMoveAsync()
        {
            Point? point;
            lock (view._inputGate)
            {
                point = view._pendingPointerMove;
                view._pendingPointerMove = null;
            }

            if (point is { } latest)
            {
                await SendInputAsync(() => view._session.SendPointerMoveAsync(
                    latest.X,
                    latest.Y)).ConfigureAwait(false);
            }

            lock (view._inputGate)
            {
                view._pointerMoveQueued = false;
                if (view._pendingPointerMove is not null)
                {
                    view._pointerMoveQueued = true;
                    QueueInputLocked(SendLatestPointerMoveAsync);
                }
            }
        }

        internal void QueuePointerButton(string button, bool isDown)
        {
            view.QueueInput(async () =>
            {
                Point? point;
                lock (view._inputGate)
                {
                    point = view._pendingPointerMove;
                    view._pendingPointerMove = null;
                }

                if (point is { } latest)
                {
                    await view._session.SendPointerMoveAsync(
                        latest.X,
                        latest.Y).ConfigureAwait(false);
                }

                await view._session.SendPointerButtonAsync(button, isDown)
                    .ConfigureAwait(false);
            });
        }

        internal async Task SendInputAsync(Func<Task> input)
        {
            try
            {
                await input().ConfigureAwait(false);
            }
            catch (InvalidOperationException) when (!view._session.IsConnected)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

}

