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

internal sealed class ReachClientViewPointer(ReachClientView view)
{
        internal void OnVideoPointerPressed(object? sender, PointerPressedEventArgs args)
        {
            view._videoSurface.Focus();
            if (args.Pointer.Type == PointerType.Touch)
            {
                view._touchPoints[args.Pointer.Id] = args.GetPosition(view._videoSurface);
                if (view._touchPoints.Count >= 2)
                {
                    view.CancelTouchLongPress();
                    if (view._touchRemoteButtonDown)
                    {
                        view.QueuePointerButton("Left", false);
                        view._touchRemoteButtonDown = false;
                    }

                    view.BeginTouchGesture();
                    args.Pointer.Capture(view._videoImage);
                    args.Handled = true;
                    return;
                }

                if (view.TryGetRemotePoint(args, out _, out _))
                {
                    view._touchPressPoint = args.GetPosition(view._videoSurface);
                    view._touchLongPressFired = false;
                    view.StartTouchLongPress();
                }

                args.Pointer.Capture(view._videoSurface);
                args.Handled = true;
                return;
            }

            if (!view.TryGetRemotePoint(args, out var x, out var y))
                return;

            var point = args.GetCurrentPoint(view._videoSurface);
            var button = ReachClientKeyMap.GetPressedButton(point.Properties, args.Pointer.Type);
            if (button is null)
                return;

            view.QueueInput(() => view._session.SendPointerMoveAsync(x, y));
            view.QueueInput(() => view._session.SendPointerButtonAsync(button, true));
            args.Pointer.Capture(view._videoSurface);
            args.Handled = true;
        }

        internal void OnVideoPointerMoved(object? sender, PointerEventArgs args)
        {
            if (args.Pointer.Type == PointerType.Touch)
            {
                view._touchPoints[args.Pointer.Id] = args.GetPosition(view._videoSurface);
                if (view._touchPoints.Count >= 2)
                {
                    view.UpdateTouchGesture();
                    args.Handled = true;
                    return;
                }

                if (view._touchGestureActive)
                {
                    args.Handled = true;
                    return;
                }

                if (!view._touchLongPressFired
                    && !view._touchRemoteButtonDown
                    && view.Distance(
                        view._touchPressPoint,
                        view._touchPoints[args.Pointer.Id]) > 8)
                {
                    view.CancelTouchLongPress();
                    if (view.TryGetRemotePoint(args, out var pressX, out var pressY))
                    {
                        view.QueueInput(async () =>
                        {
                            await view._session.SendPointerMoveAsync(pressX, pressY);
                            await view._session.SendPointerButtonAsync("Left", true);
                        });
                        view._touchRemoteButtonDown = true;
                    }
                }
            }

            if (!view.TryGetRemotePoint(args, out var x, out var y))
                return;

            view.QueuePointerMove(x, y);
            args.Handled = true;
        }

        internal void OnVideoPointerReleased(object? sender, PointerReleasedEventArgs args)
        {
            if (args.Pointer.Type == PointerType.Touch)
            {
                view.CancelTouchLongPress();
                view._touchPoints.Remove(args.Pointer.Id);
                if (view._touchPoints.Count == 0)
                {
                    if (view._touchRemoteButtonDown)
                        view.QueuePointerButton("Left", false);
                    else if (!view._touchLongPressFired
                        && view.TryGetRemotePoint(args, out var tapX, out var tapY))
                    {
                        view.QueueTouchTap(tapX, tapY);
                    }

                    view._touchRemoteButtonDown = false;
                    view._touchLongPressFired = false;
                    view._touchGestureActive = false;
                    view.ResetVideoPanIfUnzoomed();
                }
                else if (view._touchPoints.Count < 2)
                {
                    view._touchGestureActive = true;
                }

                if (args.Pointer.Captured == view._videoSurface
                    || args.Pointer.Captured == view._videoImage)
                    args.Pointer.Capture(null);
                args.Handled = true;
                return;
            }

            var button = args.InitialPressMouseButton switch
            {
                MouseButton.Left => "Left",
                MouseButton.Right => "Right",
                MouseButton.Middle => "Middle",
                _ when args.Pointer.Type == PointerType.Touch => "Left",
                _ => null,
            };
            if (button is null)
                return;

            if (args.Pointer.Captured == view._videoSurface)
                args.Pointer.Capture(null);
            view.QueuePointerButton(button, false);
            args.Handled = true;
        }

        internal void StartTouchLongPress()
        {
            view.CancelTouchLongPress();
            var cancellation = new CancellationTokenSource();
            view._touchLongPressCancellation = cancellation;
            _ = FireTouchLongPressAsync(cancellation);
        }

        internal async Task FireTouchLongPressAsync(
            CancellationTokenSource owner)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(550), owner.Token)
                    .ConfigureAwait(false);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!ReferenceEquals(view._touchLongPressCancellation, owner)
                        || view._touchPoints.Count != 1
                        || view._touchGestureActive
                        || view._touchRemoteButtonDown)
                    {
                        return;
                    }

                    if (!view.TryGetRemotePoint(
                            view._touchPressPoint,
                            out var x,
                            out var y))
                    {
                        return;
                    }

                    view._touchLongPressFired = true;
                    view.QueueInput(async () =>
                    {
                        await view._session.SendPointerMoveAsync(x, y);
                        await view._session.SendPointerButtonAsync("Right", true);
                        await view._session.SendPointerButtonAsync("Right", false);
                    });
                    view.OnStatusChanged("Right-click sent.");
                });
            }
            catch (OperationCanceledException) when (owner.IsCancellationRequested)
            {
            }
            finally
            {
                _ = Interlocked.CompareExchange(
                    ref view._touchLongPressCancellation,
                    null,
                    owner);
                owner.Dispose();
            }
        }

        internal void CancelTouchLongPress()
        {
            var cancellation = Interlocked.Exchange(
                ref view._touchLongPressCancellation,
                null);
            cancellation?.Cancel();
        }

        internal void QueueTouchTap(double x, double y)
        {
            view.QueueInput(async () =>
            {
                await view._session.SendPointerMoveAsync(x, y);
                await view._session.SendPointerButtonAsync("Left", true);
                await view._session.SendPointerButtonAsync("Left", false);
            });
        }

        internal void BeginTouchGesture()
        {
            var points = view._touchPoints.Values.Take(2).ToArray();
            if (points.Length < 2)
                return;

            view._touchGestureActive = true;
            view._gestureStartDistance = view.Distance(points[0], points[1]);
            if (view._gestureStartDistance < 1)
                view._gestureStartDistance = 1;
            view._gestureStartCenter = view.Midpoint(points[0], points[1]);
            view._lastGestureCenter = view._gestureStartCenter;
            view._gestureStartZoom = view._videoZoom;
            view._gestureStartPanX = view._videoTranslation.X;
            view._gestureStartPanY = view._videoTranslation.Y;
        }

        internal void UpdateTouchGesture()
        {
            if (!view._touchGestureActive)
                view.BeginTouchGesture();

            var points = view._touchPoints.Values.Take(2).ToArray();
            if (points.Length < 2)
                return;

            var distance = Math.Max(1, view.Distance(points[0], points[1]));
            var center = view.Midpoint(points[0], points[1]);
            if (view._scrollModeEnabled)
            {
                var scrollDelta = center.Y - view._lastGestureCenter.Y;
                if (Math.Abs(scrollDelta) >= 2)
                {
                    view.QueueInput(() => view._session.SendPointerWheelAsync(
                        (int)Math.Round(-scrollDelta * 6)));
                    view._lastGestureCenter = center;
                }
            }

            view._videoZoom = Math.Clamp(
                view._gestureStartZoom * distance / view._gestureStartDistance,
                1,
                4);
            view.ApplyVideoTransform(
                view._videoZoom,
                view._gestureStartPanX + center.X - view._gestureStartCenter.X,
                view._gestureStartPanY + center.Y - view._gestureStartCenter.Y);
        }

        internal void ApplyVideoTransform(double zoom, double panX, double panY) =>
            ReachClientViewTransform.Apply(view, zoom, panX, panY);

        internal void ResetVideoPanIfUnzoomed() =>
            ReachClientViewTransform.ResetPanIfUnzoomed(view);

        internal static double Distance(Point first, Point second) =>
            ReachClientViewTransform.Distance(first, second);

        internal static Point Midpoint(Point first, Point second) =>
            ReachClientViewTransform.Midpoint(first, second);

        internal void OnVideoPointerWheel(object? sender, PointerWheelEventArgs args) =>
            ReachClientViewTransform.OnWheel(view, sender, args);
}

