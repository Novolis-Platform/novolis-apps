using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CursorRemote.Protocol;
using CursorRemote.Services;

namespace CursorRemote.Ui;

public sealed class RemoteView : UserControl
{
    private const double MinimumZoom = 1;
    private const double MaximumZoom = 8;
    private const double TapSlop = 16;

    private readonly IRemoteControlSession _session;
    private readonly TextBlock _status = RemoteTheme.Muted(string.Empty);
    private readonly TextBlock _connectStatus = RemoteTheme.Muted(string.Empty);
    private readonly TextBlock _hostDetails = RemoteTheme.Muted(string.Empty);
    private readonly Image _screenImage = new()
    {
        Stretch = Stretch.Uniform,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
        RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
    };
    private readonly Border _screenViewport = new()
    {
        Background = RemotePalette.PanelBrush,
        Padding = new Thickness(4),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
        ClipToBounds = true,
        Child = new TextBlock
        {
            Text = "Connect to see the PC screen.",
            Foreground = RemotePalette.MutedBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        },
    };
    private readonly TextBox _endpoint = RemoteTheme.TextBox("PC endpoint, for example http://100.64.0.1:18790");
    private readonly TextBox _token = RemoteTheme.TextBox("Pairing token");
    private readonly TextBox _textInput = RemoteTheme.TextBox("Type text into Cursor");
    private readonly TextBox _hostEndpoint = RemoteTheme.TextBox();
    private readonly TextBox _hostToken = RemoteTheme.TextBox();
    private readonly StackPanel _connectPanel = new() { Spacing = 12 };
    private readonly Grid _remotePanel = new();
    private Bitmap? _screenBitmap;
    private DispatcherTimer? _refreshTimer;
    private double _zoom = 1;
    private double _panX;
    private double _panY;
    private double _pinchBaseZoom = 1;
    private bool _gestureActive;
    private Point? _pressPoint;

    public RemoteView(IRemoteControlSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Background = RemotePalette.WindowBrush;
        Focusable = true;
        _session.Changed += OnSessionChanged;

        if (_session.IsHost)
            Content = BuildHostPanel();
        else
            Content = BuildClientPanel();

        AttachedToVisualTree += OnAttached;
        DetachedFromVisualTree += OnDetached;
        UpdateUi();
    }

    private Control BuildHostPanel()
    {
        _hostEndpoint.IsReadOnly = true;
        _hostToken.IsReadOnly = true;
        _hostEndpoint.FontFamily = RemotePalette.MonoFont;
        _hostToken.FontFamily = RemotePalette.MonoFont;

        var copyEndpoint = RemoteTheme.Button("Copy endpoint", RemoteButtonKind.Secondary);
        copyEndpoint.Click += async (_, _) => await CopyAsync(_hostEndpoint.Text);
        var copyToken = RemoteTheme.Button("Copy token", RemoteButtonKind.Secondary);
        copyToken.Click += async (_, _) => await CopyAsync(_hostToken.Text);

        var endpointRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
            Children =
            {
                _hostEndpoint,
                copyEndpoint,
            },
        };
        Grid.SetColumn(copyEndpoint, 1);

        var tokenRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
            Children =
            {
                _hostToken,
                copyToken,
            },
        };
        Grid.SetColumn(copyToken, 1);

        var card = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                RemoteTheme.Label("PC HOST"),
                RemoteTheme.Title("Cursor Remote", 36),
                RemoteTheme.Body(
                    "Keep Tailscale active and leave the Windows session unlocked. " +
                    "Enter the endpoint and token in the Android controller."),
                RemoteTheme.Label("ENDPOINT"),
                endpointRow,
                RemoteTheme.Label("PAIRING TOKEN"),
                tokenRow,
                _status,
                _hostDetails,
            },
        };

        return new Border
        {
            Background = RemotePalette.PanelBrush,
            Padding = new Thickness(28),
            Margin = new Thickness(24),
            MaxWidth = 760,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = card,
        };
    }

    private Control BuildClientPanel()
    {
        _endpoint.Text = "http://100.106.124.1:18790";
        _endpoint.FontFamily = RemotePalette.MonoFont;
        _token.FontFamily = RemotePalette.MonoFont;

        var connect = RemoteTheme.Button("Connect", RemoteButtonKind.Primary);
        connect.Click += async (_, _) => await ConnectAsync();
        _connectPanel.Children.Add(RemoteTheme.Label("PHONE CONTROLLER"));
        _connectPanel.Children.Add(RemoteTheme.Title("Cursor Remote", 36));
        _connectPanel.Children.Add(RemoteTheme.Body(
            "The phone reaches the PC through Tailscale. " +
            "The PC app must be running and the Windows session must be unlocked."));
        _connectPanel.Children.Add(RemoteTheme.Label("PC ENDPOINT"));
        _connectPanel.Children.Add(_endpoint);
        _connectPanel.Children.Add(RemoteTheme.Label("PAIRING TOKEN"));
        _connectPanel.Children.Add(_token);
        _connectPanel.Children.Add(connect);
        _connectPanel.Children.Add(_connectStatus);

        ConfigureScreenGestures();
        _remotePanel.RowDefinitions = new RowDefinitions("Auto,*,Auto");
        _remotePanel.RowSpacing = 8;

        var focus = RemoteTheme.Button("Focus Cursor", RemoteButtonKind.Primary);
        focus.Click += async (_, _) => await RunAsync(
            () => _session.FocusCursorAsync());
        var refresh = RemoteTheme.Button("Refresh", RemoteButtonKind.Secondary);
        refresh.Click += async (_, _) => await RunAsync(
            () => _session.RefreshAsync());
        var resetView = RemoteTheme.Button("Reset view", RemoteButtonKind.Secondary);
        resetView.Click += (_, _) => ResetViewTransform();
        var disconnect = RemoteTheme.Button("Disconnect", RemoteButtonKind.Quiet);
        disconnect.Click += async (_, _) => await _session.DisconnectAsync();

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { focus, refresh, resetView, disconnect, _status },
        };
        Grid.SetRow(toolbar, 0);
        _remotePanel.Children.Add(toolbar);

        Grid.SetRow(_screenViewport, 1);
        _remotePanel.Children.Add(_screenViewport);

        var controls = BuildInputPanel();
        Grid.SetRow(controls, 2);
        _remotePanel.Children.Add(controls);

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            Margin = new Thickness(16),
        };
        Grid.SetRow(_connectPanel, 0);
        root.Children.Add(new Border
        {
            Background = RemotePalette.PanelBrush,
            Padding = new Thickness(24),
            MaxWidth = 680,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = _connectPanel,
        });
        Grid.SetRow(_remotePanel, 0);
        root.Children.Add(_remotePanel);
        Content = root;
        return root;
    }

    private void ConfigureScreenGestures()
    {
        _screenViewport.GestureRecognizers.Add(new PinchGestureRecognizer());
        _screenViewport.GestureRecognizers.Add(new ScrollGestureRecognizer
        {
            CanHorizontallyScroll = true,
            CanVerticallyScroll = true,
            IsScrollInertiaEnabled = false,
        });
        _screenViewport.AddHandler(InputElement.PinchEvent, OnScreenPinch);
        _screenViewport.AddHandler(InputElement.PinchEndedEvent, OnScreenPinchEnded);
        _screenViewport.AddHandler(InputElement.ScrollGestureEvent, OnScreenScroll);
        _screenViewport.PointerPressed += OnScreenPointerPressed;
        _screenViewport.PointerReleased += OnScreenPointerReleased;
    }

    private Control BuildInputPanel()
    {
        var sendText = RemoteTheme.Button("Send text", RemoteButtonKind.Primary);
        sendText.Click += async (_, _) => await SendTextAsync();

        var textRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
            Children =
            {
                _textInput,
                sendText,
            },
        };
        Grid.SetColumn(sendText, 1);

        var keyRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
        };
        foreach (var key in new[] { "Escape", "Tab", "Enter", "Control+Enter", "Control+S" })
        {
            var button = RemoteTheme.Button(key.Replace("Control+", "Ctrl+"), RemoteButtonKind.Secondary);
            button.Click += async (_, _) => await RunAsync(
                () => _session.SendKeyAsync(key));
            keyRow.Children.Add(button);
        }

        return new StackPanel
        {
            Spacing = 8,
            Children = { textRow, keyRow },
        };
    }

    private void OnScreenPinch(object? sender, PinchEventArgs args)
    {
        _gestureActive = true;
        _zoom = Math.Clamp(_pinchBaseZoom * args.Scale, MinimumZoom, MaximumZoom);
        if (_zoom <= MinimumZoom)
        {
            _panX = 0;
            _panY = 0;
        }

        ApplyViewTransform();
        args.Handled = true;
    }

    private void OnScreenPinchEnded(object? sender, PinchEndedEventArgs args)
    {
        _pinchBaseZoom = _zoom;
        args.Handled = true;
    }

    private void OnScreenScroll(object? sender, ScrollGestureEventArgs args)
    {
        if (_zoom <= MinimumZoom)
            return;

        _gestureActive = true;
        _panX += args.Delta.X;
        _panY += args.Delta.Y;
        ApplyViewTransform();
        args.Handled = true;
    }

    private void OnScreenPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        _gestureActive = false;
        _pressPoint = args.GetPosition(_screenViewport);
    }

    private async void OnScreenPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        var frame = _session.Screen;
        if (frame is null || _gestureActive || _pressPoint is null)
            return;

        var point = args.GetPosition(_screenViewport);
        var dx = point.X - _pressPoint.Value.X;
        var dy = point.Y - _pressPoint.Value.Y;
        _pressPoint = null;
        if ((dx * dx) + (dy * dy) > TapSlop * TapSlop)
            return;

        if (!RemoteCoordinateMapper.TryMap(
                _screenViewport.Bounds.Width,
                _screenViewport.Bounds.Height,
                frame.Width,
                frame.Height,
                point.X,
                point.Y,
                out var x,
                out var y,
                _zoom,
                _panX,
                _panY))
        {
            return;
        }

        await RunAsync(() => _session.ClickAsync(x, y));
        args.Handled = true;
    }

    private void ApplyViewTransform()
    {
        _screenImage.RenderTransform = new TransformGroup
        {
            Children =
            {
                new ScaleTransform(_zoom, _zoom),
                new TranslateTransform(_panX, _panY),
            },
        };
    }

    private void ResetViewTransform()
    {
        _zoom = 1;
        _pinchBaseZoom = 1;
        _panX = 0;
        _panY = 0;
        ApplyViewTransform();
    }

    private async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(_endpoint.Text)
            || string.IsNullOrWhiteSpace(_token.Text))
        {
            _status.Text = "Endpoint and token are required.";
            return;
        }

        await RunAsync(() => _session.ConnectAsync(
            _endpoint.Text,
            _token.Text));
    }

    private async Task SendTextAsync()
    {
        var text = _textInput.Text;
        if (string.IsNullOrEmpty(text))
            return;

        await RunAsync(async () =>
        {
            await _session.SendTextAsync(text);
            _textInput.Text = string.Empty;
            await _session.RefreshAsync();
        });
    }

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _status.Text = $"Error: {exception.Message}";
            _connectStatus.Text = _status.Text;
        }
    }

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        if (_session.IsHost)
            return;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _refreshTimer.Tick += async (_, _) =>
        {
            if (_session.IsConnected)
                await RunAsync(() => _session.RefreshAsync());
        };
        _refreshTimer.Start();
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        _refreshTimer?.Stop();
        _refreshTimer = null;
    }

    private void OnSessionChanged(object? sender, EventArgs args) =>
        Dispatcher.UIThread.Post(UpdateUi);

    private void UpdateUi()
    {
        _status.Text = _session.Status;
        _connectStatus.Text = _session.Status;
        if (_session.IsHost)
        {
            var connection = _session.Connection;
            _hostEndpoint.Text = connection?.Endpoint ?? "Starting host…";
            _hostToken.Text = connection?.Token ?? "Starting host…";
            _hostDetails.Text = connection?.TailscaleAddress is { Length: > 0 } address
                ? $"Tailscale address: {address} · protocol {connection.ProtocolVersion}"
                : "Tailscale was not detected. Start Tailscale and restart this host.";
            return;
        }

        if (!_session.IsConnected)
            ResetViewTransform();

        _connectPanel.IsVisible = !_session.IsConnected;
        _remotePanel.IsVisible = _session.IsConnected;
        UpdateScreen();
    }

    private void UpdateScreen()
    {
        var frame = _session.Screen;
        if (frame is null)
        {
            _screenImage.Source = null;
            if (_screenViewport.Child is Image)
                _screenViewport.Child = new TextBlock
                {
                    Text = "Connect to see the PC screen.",
                    Foreground = RemotePalette.MutedBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
            return;
        }

        using var stream = new MemoryStream(frame.Png, writable: false);
        var next = new Bitmap(stream);
        _screenBitmap?.Dispose();
        _screenBitmap = next;
        _screenImage.Source = next;
        ApplyViewTransform();
        _screenViewport.Child = _screenImage;
    }

    private async Task CopyAsync(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(text);
    }
}
