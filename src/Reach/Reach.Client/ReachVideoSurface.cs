using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Novolis.Video;

namespace Novolis.Reach.Client;

/// <summary>Renders Reach video directly from one UI-owned bitmap.</summary>
internal sealed class ReachVideoSurface : Control
{
    private readonly object _bitmapGate = new();
    private WriteableBitmap? _bitmap;

    /// <summary>Presents a decoded BGRA frame.</summary>
    public void Present(RawVideoFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (Dispatcher.UIThread.CheckAccess())
            PresentCore(frame);
        else
            Dispatcher.UIThread.Post(
                () => PresentCore(frame),
                DispatcherPriority.Background);
    }

    /// <summary>Removes the current video frame.</summary>
    public void Clear()
    {
        if (Dispatcher.UIThread.CheckAccess())
            ClearCore();
        else
            Dispatcher.UIThread.Post(ClearCore);
    }

    private void PresentCore(RawVideoFrame frame)
    {
        lock (_bitmapGate)
        {
            if (_bitmap is null
                || _bitmap.PixelSize.Width != frame.Width
                || _bitmap.PixelSize.Height != frame.Height)
            {
                _bitmap?.Dispose();
                _bitmap = new WriteableBitmap(
                    new PixelSize(frame.Width, frame.Height),
                    new Vector(96, 96),
                    PixelFormats.Bgra8888,
                    AlphaFormat.Opaque);
            }

            using var locked = _bitmap.Lock();
            var rowBytes = Math.Min(frame.Width * 4, frame.Stride);
            for (var row = 0; row < frame.Height; row++)
            {
                if (locked.Address == IntPtr.Zero)
                    throw new InvalidOperationException(
                        "Reach video bitmap was not writable.");

                Marshal.Copy(
                    frame.Pixels,
                    row * frame.Stride,
                    IntPtr.Add(locked.Address, row * locked.RowBytes),
                    rowBytes);
            }
        }

        InvalidateVisual();
    }

    private void ClearCore()
    {
        lock (_bitmapGate)
        {
            _bitmap?.Dispose();
            _bitmap = null;
        }

        InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        WriteableBitmap? bitmap;
        lock (_bitmapGate)
            bitmap = _bitmap;

        var bounds = Bounds;
        if (bitmap is null)
        {
            context.FillRectangle(Brushes.Black, new Rect(bounds.Size));
            return;
        }

        var source = new Rect(
            new Size(bitmap.PixelSize.Width, bitmap.PixelSize.Height));
        var scale = Math.Min(
            bounds.Width / source.Width,
            bounds.Height / source.Height);
        if (scale <= 0)
            return;

        var size = source.Size * scale;
        var top = OperatingSystem.IsAndroid()
            ? 0
            : Math.Max(0, (bounds.Height - size.Height) / 2);
        var target = new Rect(
            Math.Max(0, (bounds.Width - size.Width) / 2),
            top,
            size.Width,
            size.Height);
        context.DrawImage(bitmap, source, target);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(
        VisualTreeAttachmentEventArgs e)
    {
        ClearCore();
        base.OnDetachedFromVisualTree(e);
    }
}
