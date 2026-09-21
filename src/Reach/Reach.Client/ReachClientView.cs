using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Novolis.Reach.Protocol;
using Novolis.Video;
using System.Runtime.InteropServices;

namespace Novolis.Reach.Client;

/// <summary>Shared Avalonia client surface for endpoint selection and session status.</summary>
public sealed class ReachClientView : UserControl
{
    private readonly ReachClientSession _session;
    private readonly TextBox _endpoint;
    private readonly TextBlock _status;
    private readonly TextBlock _capabilities;
    private readonly Button _connect;
    private readonly Image _videoImage;
    private readonly IReachVideoPresenter _presenter;
    private readonly IReachAudioPresenter _audioPresenter;

    /// <summary>Creates the shared client surface.</summary>
    public ReachClientView(
        ReachClientSession session,
        IReachVideoPresenter? presenter = null,
        IReachAudioPresenter? audioPresenter = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _session.StatusChanged += OnStatusChanged;
        _presenter = presenter ?? new NullReachVideoPresenter();
        _session.VideoFrameReceived += _presenter.Present;
        _presenter.FrameDecoded += OnFrameDecoded;
        _audioPresenter = audioPresenter ?? new NullReachAudioPresenter();
        _session.AudioFrameReceived += _audioPresenter.Present;

        _endpoint = new TextBox
        {
            Text = "127.0.0.1:19800",
            PlaceholderText = "Host endpoint",
            MinWidth = 260,
        };
        _connect = new Button
        {
            Content = "Connect",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _connect.Click += ConnectClicked;
        _status = new TextBlock
        {
            Text = "Not connected",
            TextWrapping = TextWrapping.Wrap,
        };
        _capabilities = new TextBlock
        {
            Text = "Capabilities: not negotiated",
            TextWrapping = TextWrapping.Wrap,
        };

        _videoImage = new Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        var videoSurface = new Border
        {
            Background = Brushes.Black,
            MinHeight = 360,
            Child = _videoImage,
        };

        Content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"),
            Margin = new global::Avalonia.Thickness(24),
            RowSpacing = 12,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { _endpoint, _connect },
                },
                _status,
                _capabilities,
                videoSurface,
            },
        };
        Grid.SetRow(_status, 1);
        Grid.SetRow(_capabilities, 2);
        Grid.SetRow(videoSurface, 3);
    }

    private async void ConnectClicked(
        object? sender,
        global::Avalonia.Interactivity.RoutedEventArgs args)
    {
        _connect.IsEnabled = false;
        try
        {
            await _session.ConnectAsync(
                _endpoint.Text ?? string.Empty,
                ResolvePlatform(),
                Environment.MachineName);
            var capabilities = _session.NegotiatedCapabilities;
            _capabilities.Text = capabilities is null
                ? "Capabilities: none"
                : $"Capabilities: {capabilities.Features}; "
                  + $"video={string.Join(",", capabilities.OfferedVideoCodecs)}";
        }
        catch (Exception exception)
        {
            _status.Text = $"Connection failed: {exception.Message}";
        }
        finally
        {
            _connect.IsEnabled = true;
        }
    }

    private void OnStatusChanged(string status)
    {
        if (Dispatcher.UIThread.CheckAccess())
            _status.Text = status;
        else
            Dispatcher.UIThread.Post(() => _status.Text = status);
    }

    private void OnFrameDecoded(RawVideoFrame frame)
    {
        if (frame.Format != VideoPixelFormat.Bgra32)
            return;

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnFrameDecoded(frame));
            return;
        }

        var bitmap = new WriteableBitmap(
            new PixelSize(frame.Width, frame.Height),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Opaque);
        using (var locked = bitmap.Lock())
        {
            var rowBytes = Math.Min(frame.Width * 4, frame.Stride);
            for (var row = 0; row < frame.Height; row++)
            {
                if (locked.Address == IntPtr.Zero)
                    throw new InvalidOperationException("Video bitmap was not writable.");
                Marshal.Copy(
                    frame.Pixels,
                    row * frame.Stride,
                    IntPtr.Add(locked.Address, row * locked.RowBytes),
                    rowBytes);
            }
        }

        var previous = _videoImage.Source;
        _videoImage.Source = bitmap;
        (previous as IDisposable)?.Dispose();
    }

    private static ReachPlatform ResolvePlatform() =>
        OperatingSystem.IsAndroid()
            ? ReachPlatform.Android
            : OperatingSystem.IsLinux()
                ? ReachPlatform.Linux
                : ReachPlatform.Windows;
}
