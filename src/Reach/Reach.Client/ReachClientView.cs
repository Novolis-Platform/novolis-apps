using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    private readonly Button _discover;
    private readonly Button _connect;
    private readonly Image _videoImage;
    private readonly IReachVideoPresenter _presenter;
    private readonly IReachAudioPresenter _audioPresenter;
    private readonly HashSet<Key> _pressedKeys = [];
    private int _videoWidth;
    private int _videoHeight;
    private int _selectedDisplayLeft;
    private int _selectedDisplayTop;
    private bool _discoveryActive;

    /// <summary>Creates the shared client surface.</summary>
    public ReachClientView(
        ReachClientSession session,
        IReachVideoPresenter? presenter = null,
        IReachAudioPresenter? audioPresenter = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _session.StatusChanged += OnStatusChanged;
        _session.DisplayTopologyReceived += OnDisplayTopology;
        _session.VideoStreamStarted += OnVideoStreamStarted;
        _session.ClipboardContentReceived += OnClipboardContent;
        _presenter = presenter ?? new NullReachVideoPresenter();
        _session.VideoFrameReceived += _presenter.Present;
        _presenter.FrameDecoded += OnFrameDecoded;
        if (_presenter is IReachKeyFrameRequester keyFrameRequester)
            keyFrameRequester.KeyFrameRequested += OnKeyFrameRequested;
        _audioPresenter = audioPresenter ?? new NullReachAudioPresenter();
        _session.AudioFrameReceived += _audioPresenter.Present;

        _endpoint = new TextBox
        {
            Text = ResolveDefaultEndpoint(),
            PlaceholderText = "Searching for Reach hosts...",
            MinWidth = 260,
        };
        _endpoint.TextChanged += EndpointTextChanged;
        _discover = new Button
        {
            Content = "Discover",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _discover.Click += DiscoverClicked;
        _connect = new Button
        {
            Content = "Connect",
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = false,
        };
        _connect.Click += ConnectClicked;
        _status = new TextBlock
        {
            Text = "Searching for Reach hosts on LAN and Tailscale...",
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
            Focusable = true,
            IsHitTestVisible = true,
        };
        _videoImage.AddHandler(
            InputElement.PointerPressedEvent,
            OnVideoPointerPressed,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        _videoImage.AddHandler(
            InputElement.PointerMovedEvent,
            OnVideoPointerMoved,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        _videoImage.AddHandler(
            InputElement.PointerReleasedEvent,
            OnVideoPointerReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        _videoImage.PointerWheelChanged += OnVideoPointerWheel;
        _videoImage.KeyDown += OnVideoKeyDown;
        _videoImage.KeyUp += OnVideoKeyUp;
        _videoImage.AddHandler(
            InputElement.TextInputEvent,
            OnVideoTextInput,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
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
                    Children = { _endpoint, _discover, _connect },
                },
                _status,
                _capabilities,
                videoSurface,
            },
        };
        Grid.SetRow(_status, 1);
        Grid.SetRow(_capabilities, 2);
        Grid.SetRow(videoSurface, 3);

        _ = DiscoverHostsAsync();
    }

    private void EndpointTextChanged(object? sender, TextChangedEventArgs args)
    {
        _connect.IsEnabled = !_discoveryActive
            && !string.IsNullOrWhiteSpace(_endpoint.Text);
    }

    private async void DiscoverClicked(
        object? sender,
        global::Avalonia.Interactivity.RoutedEventArgs args)
    {
        await DiscoverHostsAsync();
    }

    private async Task DiscoverHostsAsync()
    {
        _discoveryActive = true;
        _discover.IsEnabled = false;
        _connect.IsEnabled = false;
        _status.Text = "Searching for Reach hosts on LAN and Tailscale...";
        try
        {
            var hosts = await ReachClientDiscovery.ScanAsync(
                    TimeSpan.FromSeconds(2));
            if (hosts.Count == 0)
            {
                _status.Text = "No Reach hosts found on LAN or Tailscale.";
                return;
            }

            foreach (var host in hosts)
            {
                _endpoint.Text = host.Endpoint;
                _status.Text = $"Connecting to {host.HostName}...";
                if (await ConnectToEndpointAsync())
                    return;
            }

            _status.Text = $"Found {hosts.Count} Reach hosts, but none accepted a connection.";
        }
        catch (Exception exception)
        {
            _status.Text = $"Discovery failed: {exception.Message}";
        }
        finally
        {
            _discoveryActive = false;
            _discover.IsEnabled = true;
            _connect.IsEnabled = !string.IsNullOrWhiteSpace(_endpoint.Text);
        }
    }

    private async void ConnectClicked(
        object? sender,
        global::Avalonia.Interactivity.RoutedEventArgs args)
    {
        await ConnectToEndpointAsync();
    }

    private async Task<bool> ConnectToEndpointAsync()
    {
        _connect.IsEnabled = false;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await _session.ConnectAsync(
                _endpoint.Text ?? string.Empty,
                ResolvePlatform(),
                Environment.MachineName,
                timeout.Token);
            var capabilities = _session.NegotiatedCapabilities;
            _capabilities.Text = capabilities is null
                ? "Capabilities: none"
                : $"Capabilities: {capabilities.Features}; "
                  + $"video={string.Join(",", capabilities.OfferedVideoCodecs)}";
            return true;
        }
        catch (Exception exception)
        {
            _status.Text = $"Connection failed: {exception.Message}";
            return false;
        }
        finally
        {
            _connect.IsEnabled = !_discoveryActive
                && !string.IsNullOrWhiteSpace(_endpoint.Text);
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

        _videoWidth = frame.Width;
        _videoHeight = frame.Height;
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

    private void OnKeyFrameRequested() =>
        QueueInput(() => _session.RequestKeyFrameAsync());

    private void OnDisplayTopology(ReachDisplayTopology topology)
    {
        var display = topology.Displays.FirstOrDefault();
        if (display is null)
            return;

        void Apply()
        {
            _selectedDisplayLeft = display.Left;
            _selectedDisplayTop = display.Top;
        }

        if (Dispatcher.UIThread.CheckAccess())
            Apply();
        else
            Dispatcher.UIThread.Post(Apply);
    }

    private void OnVideoStreamStarted(ReachVideoStreamStart stream)
    {
        if (stream.Width > 0)
            _videoWidth = stream.Width;
        if (stream.Height > 0)
            _videoHeight = stream.Height;
    }

    private void OnClipboardContent(ReachClipboardContent content)
    {
        var description = content.Format switch
        {
            "text" when content.Text is { Length: > 0 } => "Remote clipboard text received.",
            "files" when content.Files is { Length: > 0 } =>
                $"Remote clipboard: {content.Files.Length} file(s) received.",
            _ => "Remote clipboard content received.",
        };
        OnStatusChanged(description);
    }

    private void OnVideoPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        _videoImage.Focus();
        if (!TryGetRemotePoint(args, out var x, out var y))
            return;

        var point = args.GetCurrentPoint(_videoImage);
        var button = GetPressedButton(point.Properties);
        if (button is null)
            return;

        QueueInput(() => _session.SendPointerMoveAsync(x, y));
        QueueInput(() => _session.SendPointerButtonAsync(button, true));
        args.Pointer.Capture(_videoImage);
        args.Handled = true;
    }

    private void OnVideoPointerMoved(object? sender, PointerEventArgs args)
    {
        if (!TryGetRemotePoint(args, out var x, out var y))
            return;

        QueueInput(() => _session.SendPointerMoveAsync(x, y));
        args.Handled = true;
    }

    private void OnVideoPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        var button = args.InitialPressMouseButton switch
        {
            MouseButton.Left => "Left",
            MouseButton.Right => "Right",
            MouseButton.Middle => "Middle",
            _ => null,
        };
        if (button is null)
            return;

        if (args.Pointer.Captured == _videoImage)
            args.Pointer.Capture(null);
        QueueInput(() => _session.SendPointerButtonAsync(button, false));
        args.Handled = true;
    }

    private void OnVideoPointerWheel(object? sender, PointerWheelEventArgs args)
    {
        var delta = (int)Math.Round(args.Delta.Y * 120);
        if (delta == 0)
            return;

        QueueInput(() => _session.SendPointerWheelAsync(delta));
        args.Handled = true;
    }

    private void OnVideoKeyDown(object? sender, KeyEventArgs args)
    {
        if (IsPrintableKey(args.Key) && args.KeyModifiers == KeyModifiers.None)
            return;
        if (!TryGetVirtualKey(args.Key, out var virtualKey)
            || !_pressedKeys.Add(args.Key))
        {
            return;
        }

        QueueInput(() => _session.SendKeyAsync(virtualKey, true));
        args.Handled = true;
    }

    private void OnVideoKeyUp(object? sender, KeyEventArgs args)
    {
        if (!_pressedKeys.Remove(args.Key)
            || !TryGetVirtualKey(args.Key, out var virtualKey))
        {
            return;
        }

        QueueInput(() => _session.SendKeyAsync(virtualKey, false));
        args.Handled = true;
    }

    private void OnVideoTextInput(object? sender, TextInputEventArgs args)
    {
        if (args.Text is not { Length: > 0 })
            return;

        QueueInput(() => _session.SendTextInputAsync(args.Text));
        args.Handled = true;
    }

    private bool TryGetRemotePoint(
        PointerEventArgs args,
        out double x,
        out double y)
    {
        x = 0;
        y = 0;
        if (_videoWidth <= 0 || _videoHeight <= 0)
            return false;

        var bounds = _videoImage.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return false;

        var scale = Math.Min(
            bounds.Width / _videoWidth,
            bounds.Height / _videoHeight);
        var renderedWidth = _videoWidth * scale;
        var renderedHeight = _videoHeight * scale;
        var originX = (bounds.Width - renderedWidth) / 2;
        var originY = (bounds.Height - renderedHeight) / 2;
        var point = args.GetPosition(_videoImage);
        if (point.X < originX
            || point.Y < originY
            || point.X >= originX + renderedWidth
            || point.Y >= originY + renderedHeight)
        {
            return false;
        }

        x = _selectedDisplayLeft + Math.Clamp(
            (point.X - originX) / scale,
            0,
            _videoWidth - 1);
        y = _selectedDisplayTop + Math.Clamp(
            (point.Y - originY) / scale,
            0,
            _videoHeight - 1);
        return true;
    }

    private static string? GetPressedButton(PointerPointProperties properties)
    {
        if (properties.IsLeftButtonPressed)
            return "Left";
        if (properties.IsRightButtonPressed)
            return "Right";
        if (properties.IsMiddleButtonPressed)
            return "Middle";
        return null;
    }

    private void QueueInput(Func<Task> input)
    {
        _ = SendInputAsync(input);
    }

    private async Task SendInputAsync(Func<Task> input)
    {
        try
        {
            await input().ConfigureAwait(false);
        }
        catch (InvalidOperationException) when (!_session.IsConnected)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static bool IsPrintableKey(Key key) =>
        key is >= Key.A and <= Key.Z
            or >= Key.D0 and <= Key.D9
            or >= Key.NumPad0 and <= Key.NumPad9
            or Key.Space;

    private static bool TryGetVirtualKey(Key key, out ushort virtualKey)
    {
        virtualKey = key switch
        {
            >= Key.A and <= Key.Z => (ushort)('A' + ((int)key - (int)Key.A)),
            >= Key.D0 and <= Key.D9 => (ushort)('0' + ((int)key - (int)Key.D0)),
            >= Key.NumPad0 and <= Key.NumPad9 =>
                (ushort)(0x60 + ((int)key - (int)Key.NumPad0)),
            Key.Back => 0x08,
            Key.Tab => 0x09,
            Key.Return => 0x0D,
            Key.Escape => 0x1B,
            Key.Space => 0x20,
            Key.Left => 0x25,
            Key.Up => 0x26,
            Key.Right => 0x27,
            Key.Down => 0x28,
            Key.Insert => 0x2D,
            Key.Delete => 0x2E,
            Key.Home => 0x24,
            Key.End => 0x23,
            Key.PageUp => 0x21,
            Key.PageDown => 0x22,
            Key.LeftShift or Key.RightShift => 0x10,
            Key.LeftCtrl or Key.RightCtrl => 0x11,
            Key.LeftAlt or Key.RightAlt => 0x12,
            Key.LWin or Key.RWin => 0x5B,
            >= Key.F1 and <= Key.F12 => (ushort)(0x70 + ((int)key - (int)Key.F1)),
            Key.OemPlus or Key.Add => 0xBB,
            Key.OemMinus or Key.Subtract => 0xBD,
            Key.OemComma => 0xBC,
            Key.OemPeriod => 0xBE,
            Key.OemQuestion => 0xBF,
            Key.OemOpenBrackets => 0xDB,
            Key.OemCloseBrackets => 0xDD,
            Key.OemPipe => 0xDC,
            Key.OemSemicolon => 0xBA,
            Key.OemQuotes => 0xDE,
            Key.OemTilde => 0xC0,
            _ => 0,
        };
        return virtualKey != 0;
    }

    private static string ResolveDefaultEndpoint() =>
        OperatingSystem.IsAndroid()
            ? "10.0.2.2:19800"
            : "127.0.0.1:19800";

    private static ReachPlatform ResolvePlatform() =>
        OperatingSystem.IsAndroid()
            ? ReachPlatform.Android
            : OperatingSystem.IsLinux()
                ? ReachPlatform.Linux
                : ReachPlatform.Windows;
}
