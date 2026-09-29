using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Novolis.Reach.Protocol;
using Novolis.Video;

namespace Novolis.Reach.Client;

/// <summary>Shared Avalonia client surface for endpoint selection and session status.</summary>
public sealed class ReachClientView : UserControl
{
    private readonly ReachClientSession _session;
    private readonly TextBox _endpoint;
    private readonly TextBlock _status;
    private readonly TextBlock _capabilities;
    private readonly TextBlock _sessionPhase;
    private readonly TextBlock _performanceStatus;
    private readonly ListBox _discoveredHosts;
    private readonly Button _discover;
    private readonly Button _connect;
    private readonly Button _keyboardToggle;
    private readonly Button _fitToScreen;
    private readonly Button _resetZoom;
    private readonly Button _scrollMode;
    private readonly StackPanel _sessionToolbar;
    private readonly ReachVideoSurface _videoImage;
    private readonly Border _videoSurface;
    private readonly TextBox _remoteTextInput;
    private readonly Button _sendText;
    private readonly IReachVideoPresenter _presenter;
    private readonly IReachAudioPresenter _audioPresenter;
    private readonly HashSet<Key> _pressedKeys = [];
    private readonly Dictionary<int, Point> _touchPoints = [];
    private readonly ScaleTransform _videoScale = new(1, 1);
    private readonly TranslateTransform _videoTranslation = new();
    private readonly object _inputGate = new();
    private readonly object _frameGate = new();
    private Task _inputTail = Task.CompletedTask;
    private Point? _pendingPointerMove;
    private bool _pointerMoveQueued;
    private int _videoWidth;
    private int _videoHeight;
    private int _selectedDisplayLeft;
    private int _selectedDisplayTop;
    private int _selectedDisplayWidth;
    private int _selectedDisplayHeight;
    private RawVideoFrame? _pendingFrame;
    private bool _frameUpdateScheduled;
    private bool _androidVideoConfigured;
    private bool _touchGestureActive;
    private bool _touchRemoteButtonDown;
    private bool _touchLongPressFired;
    private Point _touchPressPoint;
    private CancellationTokenSource? _touchLongPressCancellation;
    private double _gestureStartDistance;
    private double _gestureStartZoom;
    private double _gestureStartPanX;
    private double _gestureStartPanY;
    private Point _gestureStartCenter;
    private Point _lastGestureCenter;
    private double _videoZoom = 1;
    private bool _discoveryActive;
    private bool _streamStatusShown;
    private bool _keyboardMode;
    private bool _scrollModeEnabled;
    private string _endpointValue = string.Empty;
    private int _statusPriority;
    private CancellationTokenSource? _reconnectCancellation;
    private CancellationTokenSource? _connectCancellation;
    private ReachVideoProfileController? _videoProfileController;
    private bool _sessionEnded;
    private readonly Dictionary<string, string> _discoveredHostEndpoints = [];

    /// <summary>Creates the shared client surface.</summary>
    public ReachClientView(
        ReachClientSession session,
        IReachVideoPresenter? presenter = null,
        IReachAudioPresenter? audioPresenter = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _session.StatusChanged += OnStatusChanged;
        _session.StateChanged += OnSessionStateChanged;
        _session.SessionEnded += OnSessionEnded;
        _session.MediaConnectionLost += OnMediaConnectionLost;
        _session.ConnectionLost += OnConnectionLost;
        _session.PhaseChanged += OnPhaseChanged;
        _session.PerformanceChanged += OnPerformanceChanged;
        _session.SharingStateChanged += OnSharingStateChanged;
        _session.DisplayTopologyReceived += OnDisplayTopology;
        _session.VideoStreamStarted += OnVideoStreamStarted;
        _session.VideoStreamReset += OnVideoStreamReset;
        _session.ClipboardContentReceived += OnClipboardContent;
        _presenter = presenter ?? new NullReachVideoPresenter();
        _session.VideoFrameReceived += _presenter.Present;
        _presenter.FrameDecoded += OnFrameDecoded;
        if (_presenter is IReachVideoPerformanceSource performanceSource)
            performanceSource.DecodeCompleted += _session.RecordDecodedFrame;
        if (_presenter is IReachVideoDropSource dropSource)
        {
            dropSource.FrameDropped += _session.RecordDroppedFrame;
        }
        if (_presenter is IReachKeyFrameRequester keyFrameRequester)
            keyFrameRequester.KeyFrameRequested += OnKeyFrameRequested;
        _audioPresenter = audioPresenter ?? new NullReachAudioPresenter();
        _session.AudioFrameReceived += _audioPresenter.Present;

        _endpoint = new TextBox
        {
            Name = "ReachEndpoint",
            Text = ResolveDefaultEndpoint(),
            PlaceholderText = "Searching for Reach hosts...",
            Width = OperatingSystem.IsAndroid() ? double.NaN : 260,
            MinWidth = OperatingSystem.IsAndroid() ? 0 : 260,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        _endpoint.TextChanged += EndpointTextChanged;
        if (LoadRememberedEndpoints().FirstOrDefault() is { } rememberedEndpoint)
            _endpoint.Text = rememberedEndpoint;
        _endpointValue = _endpoint.Text ?? string.Empty;
        _discover = new Button
        {
            Name = "ReachDiscover",
            Content = "Discover",
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 0,
            Padding = new global::Avalonia.Thickness(8, 4),
        };
        _discover.Click += DiscoverClicked;
        _connect = new Button
        {
            Name = "ReachConnect",
            Content = "Connect",
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = false,
            MinWidth = 0,
            Padding = new global::Avalonia.Thickness(8, 4),
        };
        _connect.Click += ConnectClicked;
        _keyboardToggle = new Button
        {
            Name = "ReachKeyboard",
            Content = "Keyboard",
            IsVisible = OperatingSystem.IsAndroid(),
            MinWidth = 0,
        };
        _keyboardToggle.Click += KeyboardToggleClicked;
        _fitToScreen = new Button
        {
            Name = "ReachFitToScreen",
            Content = "Fit",
            IsVisible = OperatingSystem.IsAndroid(),
            MinWidth = 0,
        };
        _fitToScreen.Click += (_, _) => ApplyVideoTransform(1, 0, 0);
        _resetZoom = new Button
        {
            Name = "ReachResetZoom",
            Content = "Reset zoom",
            IsVisible = OperatingSystem.IsAndroid(),
            MinWidth = 0,
        };
        _resetZoom.Click += (_, _) => ApplyVideoTransform(1, 0, 0);
        _scrollMode = new Button
        {
            Name = "ReachScrollMode",
            Content = "Scroll",
            IsVisible = OperatingSystem.IsAndroid(),
            MinWidth = 0,
        };
        _scrollMode.Click += ScrollModeClicked;
        _sessionToolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            IsVisible = OperatingSystem.IsAndroid(),
            Children =
            {
                _keyboardToggle,
                _fitToScreen,
                _resetZoom,
                _scrollMode,
            },
        };
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
        _sessionPhase = new TextBlock
        {
            Text = "Phase: Disconnected",
            TextWrapping = TextWrapping.Wrap,
        };
        _performanceStatus = new TextBlock
        {
            Text = "Performance: waiting for frames",
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        };
        _discoveredHosts = new ListBox
        {
            Name = "ReachDiscoveredHosts",
            Height = 72,
            IsVisible = false,
            SelectionMode = SelectionMode.Single,
        };
        _discoveredHosts.SelectionChanged += DiscoveredHostSelected;

        _videoImage = new ReachVideoSurface
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = true,
            RenderTransformOrigin = new RelativePoint(
                0.5,
                0.5,
                RelativeUnit.Relative),
            RenderTransform = new TransformGroup
            {
                Children = { _videoScale, _videoTranslation },
            },
        };
        _videoSurface = new Border
        {
            Name = "ReachVideoSurface",
            Background = Brushes.Black,
            Focusable = true,
            IsHitTestVisible = true,
            MinHeight = OperatingSystem.IsAndroid() ? 220 : 360,
            ClipToBounds = true,
            Child = _videoImage,
        };
        _videoSurface.KeyDown += OnVideoKeyDown;
        _videoSurface.KeyUp += OnVideoKeyUp;
        _videoSurface.AddHandler(
            InputElement.TextInputEvent,
            OnVideoTextInput,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        _videoSurface.AddHandler(
            InputElement.PointerPressedEvent,
            OnVideoPointerPressed,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        _videoSurface.AddHandler(
            InputElement.PointerMovedEvent,
            OnVideoPointerMoved,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        _videoSurface.AddHandler(
            InputElement.PointerReleasedEvent,
            OnVideoPointerReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        _videoSurface.PointerWheelChanged += OnVideoPointerWheel;
        _remoteTextInput = new TextBox
        {
            Name = "ReachRemoteTextInput",
            PlaceholderText = "Type to send to remote session",
            Width = double.NaN,
            MinWidth = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsVisible = false,
        };
        _sendText = new Button
        {
            Name = "ReachSendText",
            Content = "Send",
            IsVisible = false,
        };
        _sendText.Click += SendTextClicked;
        _remoteTextInput.KeyDown += RemoteTextKeyDown;
        var remoteTextRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
            IsVisible = OperatingSystem.IsAndroid(),
            Children = { _remoteTextInput, _sendText },
        };
        Grid.SetColumn(_sendText, 1);

        var endpointRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            ColumnSpacing = 8,
            Children = { _endpoint, _discover, _connect },
        };
        Grid.SetColumn(_discover, 1);
        Grid.SetColumn(_connect, 2);

        Content = new Grid
        {
            RowDefinitions = new RowDefinitions(
                "Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto,*"),
            Margin = new global::Avalonia.Thickness(
                OperatingSystem.IsAndroid() ? 12 : 24),
            RowSpacing = 12,
            Children =
            {
                endpointRow,
                _discoveredHosts,
                _status,
                _capabilities,
                _sessionPhase,
                _performanceStatus,
                _sessionToolbar,
                remoteTextRow,
                _videoSurface,
            },
        };
        Grid.SetRow(_discoveredHosts, 1);
        Grid.SetRow(_status, 2);
        Grid.SetRow(_capabilities, 3);
        Grid.SetRow(_sessionPhase, 4);
        Grid.SetRow(_performanceStatus, 5);
        Grid.SetRow(_sessionToolbar, 6);
        Grid.SetRow(remoteTextRow, 7);
        Grid.SetRow(_videoSurface, 8);

        _ = DiscoverHostsAsync();
    }

    private void EndpointTextChanged(object? sender, TextChangedEventArgs args)
    {
        Volatile.Write(
            ref _endpointValue,
            _endpoint.Text ?? string.Empty);
        UpdateConnectionControls();
    }

    private void DiscoveredHostSelected(
        object? sender,
        SelectionChangedEventArgs args)
    {
        if (_discoveredHosts.SelectedItem is not string label
            || !_discoveredHostEndpoints.TryGetValue(label, out var endpoint))
        {
            return;
        }

        _endpoint.Text = endpoint;
        OnStatusChanged($"Selected {label}.");
        UpdateConnectionControls();
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
                ApplyRememberedHostList();
                return;
            }

            _discoveredHostEndpoints.Clear();
            var labels = new List<string>();
            foreach (var host in hosts)
            {
                var label = $"{host.HostName} — {host.Endpoint}";
                _discoveredHostEndpoints[label] = host.Endpoint;
                labels.Add(label);
            }

            _discoveredHosts.ItemsSource = labels;
            _discoveredHosts.IsVisible = labels.Count > 0;
            foreach (var host in hosts)
            {
                _endpoint.Text = host.Endpoint;
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
            UpdateConnectionControls();
        }
    }

    private void ApplyRememberedHostList()
    {
        var remembered = LoadRememberedEndpoints().ToArray();
        _discoveredHostEndpoints.Clear();
        var labels = new List<string>();
        foreach (var endpoint in remembered)
        {
            var label = $"Remembered — {endpoint}";
            _discoveredHostEndpoints[label] = endpoint;
            labels.Add(label);
        }

        _discoveredHosts.ItemsSource = labels;
        _discoveredHosts.IsVisible = labels.Count > 0;
    }

    private async void ConnectClicked(
        object? sender,
        global::Avalonia.Interactivity.RoutedEventArgs args)
    {
        if (_session.State == ReachSessionState.Connecting)
        {
            _connectCancellation?.Cancel();
            return;
        }

        if (_session.IsConnected || _session.State == ReachSessionState.Lost)
        {
            await ReconnectToEndpointAsync();
            return;
        }

        await ConnectToEndpointAsync();
    }

    private async Task<bool> ConnectToEndpointAsync()
    {
        CancelReconnect();
        _sessionEnded = false;
        _connect.IsEnabled = false;
        _androidVideoConfigured = false;
        _streamStatusShown = false;
        ResetStatusPriority();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        _connectCancellation = timeout;
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
            RememberEndpoint(_endpoint.Text);
            _discoveredHosts.IsVisible = false;
            SetConnectedStatus();
            return true;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            OnStatusChanged("Connection cancelled.");
            return false;
        }
        catch (Exception exception)
        {
            OnStatusChanged($"Connection failed: {exception.Message}");
            return false;
        }
        finally
        {
            _ = Interlocked.CompareExchange(
                ref _connectCancellation,
                null,
                timeout);
            UpdateConnectionControls();
        }
    }

    private async Task<bool> ReconnectToEndpointAsync(
        CancellationToken cancellationToken = default,
        bool cancelExistingReconnect = true)
    {
        if (cancelExistingReconnect)
            CancelReconnect();
        _connect.IsEnabled = false;
        _sessionEnded = false;
        _androidVideoConfigured = false;
        _streamStatusShown = false;
        ResetStatusPriority();
        ClearVideoFrame();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            await _session.ReconnectAsync(timeout.Token);
            _capabilities.Text = _session.NegotiatedCapabilities is { } capabilities
                ? $"Capabilities: {capabilities.Features}; "
                  + $"video={string.Join(",", capabilities.OfferedVideoCodecs)}"
                : "Capabilities: none";
            SetConnectedStatus(reconnected: true);
            return true;
        }
        catch (Exception exception)
        {
            OnStatusChanged($"Reconnect failed: {exception.Message}");
            return false;
        }
        finally
        {
            UpdateConnectionControls();
        }
    }

    private void OnStatusChanged(string status)
    {
        var priority = GetStatusPriority(status);
        void Apply()
        {
            if (priority < _statusPriority)
                return;

            _statusPriority = priority;
            _status.Text = status;
        }

        if (Dispatcher.UIThread.CheckAccess())
            Apply();
        else
            Dispatcher.UIThread.Post(Apply);
    }

    private void ResetStatusPriority()
    {
        if (Dispatcher.UIThread.CheckAccess())
            _statusPriority = 0;
        else
            Dispatcher.UIThread.Post(() => _statusPriority = 0);
    }

    private void OnPhaseChanged(ReachConnectionPhase phase)
    {
        void Apply() => _sessionPhase.Text = $"Phase: {phase}";
        if (Dispatcher.UIThread.CheckAccess())
            Apply();
        else
            Dispatcher.UIThread.Post(Apply);
    }

    private void OnPerformanceChanged(ReachPerformanceSnapshot snapshot)
    {
        void Apply()
        {
            var frameAge = snapshot.FrameAgeP95Milliseconds is { } age
                ? $"{age:0} ms"
                : "n/a";
            var roundTrip = snapshot.InputRoundTripP95Milliseconds is { } rtt
                ? $"{rtt:0} ms"
                : "n/a";
            _performanceStatus.Text =
                $"Transport: {_session.ActiveTransport}; "
                + $"frame age p95: {frameAge}; input RTT p95: {roundTrip}; "
                + $"drops: {snapshot.DroppedFrames}; "
                + $"keyframes: {snapshot.KeyFrameRequests}";
            _performanceStatus.IsVisible =
                OperatingSystem.IsAndroid() && snapshot.ReceivedFrames > 0;
        }

        if (OperatingSystem.IsAndroid()
            && _videoProfileController is { } profileController
            && _session.IsConnected
            && profileController.Observe(
                    snapshot,
                    DateTimeOffset.UtcNow)
                is { } profile)
        {
            QueueInput(() => _session.ConfigureVideoAsync(
                profile.Width,
                profile.Height,
                profile.FramesPerSecond,
                profile.TargetBitrate));
            OnStatusChanged(
                $"Video quality adjusted to {profile.Kind} "
                + $"({profile.FramesPerSecond} FPS).");
        }

        if (Dispatcher.UIThread.CheckAccess())
            Apply();
        else
            Dispatcher.UIThread.Post(Apply);
    }

    private void SetConnectedStatus(bool reconnected = false)
    {
        if (_session.State == ReachSessionState.Streaming
            && _session.LastVideoFrameAt is not null)
        {
            var dimensions = _videoWidth > 0 && _videoHeight > 0
                ? $" ({_videoWidth}x{_videoHeight})"
                : string.Empty;
            OnStatusChanged($"Streaming remote session{dimensions}.");
            return;
        }

        OnStatusChanged(
            reconnected
                ? "Reconnected; waiting for the remote session stream..."
                : "Connected; waiting for the remote session stream...");
    }

    private static int GetStatusPriority(string status) =>
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

    private void OnSessionStateChanged(ReachSessionState state)
    {
        void Apply()
        {
            _connect.Content = state is ReachSessionState.Lost
                or ReachSessionState.Connected
                or ReachSessionState.Streaming
                ? "Reconnect"
                : "Connect";
            UpdateConnectionControls();
        }

        if (Dispatcher.UIThread.CheckAccess())
            Apply();
        else
            Dispatcher.UIThread.Post(Apply);
    }

    private void OnSessionEnded(string reason)
    {
        _sessionEnded = true;
        ClearVideoFrame();
        OnStatusChanged($"Remote session ended: {reason}");
    }

    private void OnSharingStateChanged(ReachSharingState state)
    {
        OnStatusChanged(
            state.IsPaused
                ? $"Host paused sharing: {state.Reason}"
                : "Host resumed sharing.");
    }

    private void OnMediaConnectionLost()
    {
        ClearVideoFrame();
        OnStatusChanged("Video is recovering; input remains available.");
        _ = RecoverMediaAsync();
        UpdateConnectionControls();
    }

    private void OnConnectionLost()
    {
        ClearVideoFrame();
        if (_sessionEnded)
        {
            OnStatusChanged(
                "The remote session ended. Reconnect when the host is ready.");
            UpdateConnectionControls();
            return;
        }

        OnStatusChanged("Connection lost. Reconnecting...");
        BeginReconnectLoop();
        UpdateConnectionControls();
    }

    private async Task RecoverMediaAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(5));
            await _session.RecoverMediaAsync(timeout.Token);
            OnStatusChanged("Video recovered; waiting for a fresh frame.");
        }
        catch (Exception exception)
        {
            OnStatusChanged($"Video recovery failed: {exception.Message}");
        }
    }

    private void BeginReconnectLoop()
    {
        if (_reconnectCancellation is not null
            || _session.State is not ReachSessionState.Lost)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _reconnectCancellation = cancellation;
        _ = ReconnectLoopAsync(cancellation);
    }

    private async Task ReconnectLoopAsync(CancellationTokenSource owner)
    {
        try
        {
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                var delay = TimeSpan.FromSeconds(attempt);
                OnStatusChanged(
                    $"Reconnecting in {delay.TotalSeconds:0}s "
                    + $"(attempt {attempt} of 3)...");
                await Task.Delay(delay, owner.Token);
                if (await ReconnectToEndpointAsync(
                        owner.Token,
                        cancelExistingReconnect: false))
                    return;
            }

            OnStatusChanged(
                "Reach is offline. Check the host and tap Reconnect.");
        }
        catch (OperationCanceledException) when (owner.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(
                    Interlocked.CompareExchange(
                        ref _reconnectCancellation,
                        null,
                        owner),
                    owner))
            {
                owner.Dispose();
            }
        }
    }

    private void CancelReconnect()
    {
        var cancellation = Interlocked.Exchange(
            ref _reconnectCancellation,
            null);
        cancellation?.Cancel();
        cancellation?.Dispose();
    }

    private void OnVideoStreamReset(ReachVideoStreamReset reset)
    {
        ResetVideoStreamForRecovery();
        OnStatusChanged($"Remote video stream reset at frame {reset.Sequence}.");
    }

    private void ResetVideoStreamForRecovery()
    {
        void Reset()
        {
            lock (_frameGate)
            {
                _pendingFrame = null;
                _frameUpdateScheduled = false;
            }

            _streamStatusShown = false;
            _touchPoints.Clear();
            _touchGestureActive = false;
            _touchRemoteButtonDown = false;
            CancelTouchLongPress();
            _touchLongPressFired = false;
            _videoZoom = 1;
            _videoScale.ScaleX = 1;
            _videoScale.ScaleY = 1;
            _videoTranslation.X = 0;
            _videoTranslation.Y = 0;
            if (_presenter is IReachVideoStreamResetter streamResetter)
                streamResetter.ResetStream();
        }

        if (Dispatcher.UIThread.CheckAccess())
            Reset();
        else
            Dispatcher.UIThread.Post(Reset);
    }

    private void UpdateConnectionControls()
    {
        void Apply()
        {
            if (_session.State == ReachSessionState.Connecting)
            {
                _connect.Content = "Cancel";
                _connect.IsEnabled = true;
                return;
            }

            _connect.IsEnabled = !_discoveryActive
                && !string.IsNullOrWhiteSpace(_endpoint.Text)
                && _session.State is not ReachSessionState.Connecting;
        }

        if (Dispatcher.UIThread.CheckAccess())
            Apply();
        else
            Dispatcher.UIThread.Post(Apply);
    }

    private void OnFrameDecoded(RawVideoFrame frame)
    {
        if (frame.Format != VideoPixelFormat.Bgra32
            || !_session.IsConnected)
            return;

        _videoWidth = frame.Width;
        _videoHeight = frame.Height;
        if (!_streamStatusShown)
        {
            _streamStatusShown = true;
            OnStatusChanged(
                $"Streaming remote session ({frame.Width}x{frame.Height}).");
        }
        lock (_frameGate)
        {
            _pendingFrame = frame;
            if (_frameUpdateScheduled)
                return;

            _frameUpdateScheduled = true;
        }

        Dispatcher.UIThread.Post(
            ApplyPendingFrame,
            DispatcherPriority.Render);
    }

    private void ApplyPendingFrame()
    {
        RawVideoFrame? frame;
        lock (_frameGate)
        {
            frame = _pendingFrame;
            _pendingFrame = null;
        }

        if (frame is not null)
        {
            ApplyFrame(frame);
        }

        lock (_frameGate)
        {
            if (_pendingFrame is null)
            {
                _frameUpdateScheduled = false;
                return;
            }
        }

        Dispatcher.UIThread.Post(
            ApplyPendingFrame,
            DispatcherPriority.Render);
    }

    private void ApplyFrame(RawVideoFrame frame)
    {
        var start = Stopwatch.GetTimestamp();
        _videoImage.Present(frame);
        _session.RecordPresentedFrame(
            frame.Timestamp,
            Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    }

    private void ClearVideoFrame()
    {
        void Clear()
        {
            lock (_frameGate)
            {
                _pendingFrame = null;
                _frameUpdateScheduled = false;
            }
            _videoImage.Clear();
            _videoWidth = 0;
            _videoHeight = 0;
            _androidVideoConfigured = false;
            _videoProfileController = null;
            _streamStatusShown = false;
            _touchPoints.Clear();
            _touchGestureActive = false;
            _touchRemoteButtonDown = false;
            CancelTouchLongPress();
            _touchLongPressFired = false;
            _videoZoom = 1;
            _videoScale.ScaleX = 1;
            _videoScale.ScaleY = 1;
            _videoTranslation.X = 0;
            _videoTranslation.Y = 0;
            if (_presenter is IReachVideoStreamResetter streamResetter)
                streamResetter.ResetStream();
        }

        if (Dispatcher.UIThread.CheckAccess())
            Clear();
        else
            Dispatcher.UIThread.Post(Clear);
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
            _selectedDisplayWidth = display.Width;
            _selectedDisplayHeight = display.Height;
        }

        if (Dispatcher.UIThread.CheckAccess())
            Apply();
        else
            Dispatcher.UIThread.Post(Apply);

        if (OperatingSystem.IsAndroid() && !_androidVideoConfigured)
        {
            _androidVideoConfigured = true;
            _videoProfileController = new ReachVideoProfileController(
                display,
                ResolveInitialVideoProfileKind(
                    Volatile.Read(ref _endpointValue)));
            var profile = _videoProfileController.Current;
            QueueInput(() => _session.ConfigureVideoAsync(
                profile.Width,
                profile.Height,
                profile.FramesPerSecond,
                profile.TargetBitrate));
        }
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
        _videoSurface.Focus();
        if (args.Pointer.Type == PointerType.Touch)
        {
            _touchPoints[args.Pointer.Id] = args.GetPosition(_videoSurface);
            if (_touchPoints.Count >= 2)
            {
                CancelTouchLongPress();
                if (_touchRemoteButtonDown)
                {
                    QueuePointerButton("Left", false);
                    _touchRemoteButtonDown = false;
                }

                BeginTouchGesture();
                args.Pointer.Capture(_videoImage);
                args.Handled = true;
                return;
            }

            if (TryGetRemotePoint(args, out _, out _))
            {
                _touchPressPoint = args.GetPosition(_videoSurface);
                _touchLongPressFired = false;
                StartTouchLongPress();
            }

            args.Pointer.Capture(_videoSurface);
            args.Handled = true;
            return;
        }

        if (!TryGetRemotePoint(args, out var x, out var y))
            return;

        var point = args.GetCurrentPoint(_videoSurface);
        var button = GetPressedButton(point.Properties, args.Pointer.Type);
        if (button is null)
            return;

        QueueInput(() => _session.SendPointerMoveAsync(x, y));
        QueueInput(() => _session.SendPointerButtonAsync(button, true));
        args.Pointer.Capture(_videoSurface);
        args.Handled = true;
    }

    private void OnVideoPointerMoved(object? sender, PointerEventArgs args)
    {
        if (args.Pointer.Type == PointerType.Touch)
        {
            _touchPoints[args.Pointer.Id] = args.GetPosition(_videoSurface);
            if (_touchPoints.Count >= 2)
            {
                UpdateTouchGesture();
                args.Handled = true;
                return;
            }

            if (_touchGestureActive)
            {
                args.Handled = true;
                return;
            }

            if (!_touchLongPressFired
                && !_touchRemoteButtonDown
                && Distance(
                    _touchPressPoint,
                    _touchPoints[args.Pointer.Id]) > 8)
            {
                CancelTouchLongPress();
                if (TryGetRemotePoint(args, out var pressX, out var pressY))
                {
                    QueueInput(async () =>
                    {
                        await _session.SendPointerMoveAsync(pressX, pressY);
                        await _session.SendPointerButtonAsync("Left", true);
                    });
                    _touchRemoteButtonDown = true;
                }
            }
        }

        if (!TryGetRemotePoint(args, out var x, out var y))
            return;

        QueuePointerMove(x, y);
        args.Handled = true;
    }

    private void OnVideoPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (args.Pointer.Type == PointerType.Touch)
        {
            CancelTouchLongPress();
            _touchPoints.Remove(args.Pointer.Id);
            if (_touchPoints.Count == 0)
            {
                if (_touchRemoteButtonDown)
                    QueuePointerButton("Left", false);
                else if (!_touchLongPressFired
                    && TryGetRemotePoint(args, out var tapX, out var tapY))
                {
                    QueueTouchTap(tapX, tapY);
                }

                _touchRemoteButtonDown = false;
                _touchLongPressFired = false;
                _touchGestureActive = false;
                ResetVideoPanIfUnzoomed();
            }
            else if (_touchPoints.Count < 2)
            {
                _touchGestureActive = true;
            }

            if (args.Pointer.Captured == _videoSurface
                || args.Pointer.Captured == _videoImage)
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

        if (args.Pointer.Captured == _videoSurface)
            args.Pointer.Capture(null);
        QueuePointerButton(button, false);
        args.Handled = true;
    }

    private void StartTouchLongPress()
    {
        CancelTouchLongPress();
        var cancellation = new CancellationTokenSource();
        _touchLongPressCancellation = cancellation;
        _ = FireTouchLongPressAsync(cancellation);
    }

    private async Task FireTouchLongPressAsync(
        CancellationTokenSource owner)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(550), owner.Token)
                .ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!ReferenceEquals(_touchLongPressCancellation, owner)
                    || _touchPoints.Count != 1
                    || _touchGestureActive
                    || _touchRemoteButtonDown)
                {
                    return;
                }

                if (!TryGetRemotePoint(
                        _touchPressPoint,
                        out var x,
                        out var y))
                {
                    return;
                }

                _touchLongPressFired = true;
                QueueInput(async () =>
                {
                    await _session.SendPointerMoveAsync(x, y);
                    await _session.SendPointerButtonAsync("Right", true);
                    await _session.SendPointerButtonAsync("Right", false);
                });
                OnStatusChanged("Right-click sent.");
            });
        }
        catch (OperationCanceledException) when (owner.IsCancellationRequested)
        {
        }
        finally
        {
            _ = Interlocked.CompareExchange(
                ref _touchLongPressCancellation,
                null,
                owner);
            owner.Dispose();
        }
    }

    private void CancelTouchLongPress()
    {
        var cancellation = Interlocked.Exchange(
            ref _touchLongPressCancellation,
            null);
        cancellation?.Cancel();
    }

    private void QueueTouchTap(double x, double y)
    {
        QueueInput(async () =>
        {
            await _session.SendPointerMoveAsync(x, y);
            await _session.SendPointerButtonAsync("Left", true);
            await _session.SendPointerButtonAsync("Left", false);
        });
    }

    private void BeginTouchGesture()
    {
        var points = _touchPoints.Values.Take(2).ToArray();
        if (points.Length < 2)
            return;

        _touchGestureActive = true;
        _gestureStartDistance = Distance(points[0], points[1]);
        if (_gestureStartDistance < 1)
            _gestureStartDistance = 1;
        _gestureStartCenter = Midpoint(points[0], points[1]);
        _lastGestureCenter = _gestureStartCenter;
        _gestureStartZoom = _videoZoom;
        _gestureStartPanX = _videoTranslation.X;
        _gestureStartPanY = _videoTranslation.Y;
    }

    private void UpdateTouchGesture()
    {
        if (!_touchGestureActive)
            BeginTouchGesture();

        var points = _touchPoints.Values.Take(2).ToArray();
        if (points.Length < 2)
            return;

        var distance = Math.Max(1, Distance(points[0], points[1]));
        var center = Midpoint(points[0], points[1]);
        if (_scrollModeEnabled)
        {
            var scrollDelta = center.Y - _lastGestureCenter.Y;
            if (Math.Abs(scrollDelta) >= 2)
            {
                QueueInput(() => _session.SendPointerWheelAsync(
                    (int)Math.Round(-scrollDelta * 6)));
                _lastGestureCenter = center;
            }
        }

        _videoZoom = Math.Clamp(
            _gestureStartZoom * distance / _gestureStartDistance,
            1,
            4);
        ApplyVideoTransform(
            _videoZoom,
            _gestureStartPanX + center.X - _gestureStartCenter.X,
            _gestureStartPanY + center.Y - _gestureStartCenter.Y);
    }

    private void ApplyVideoTransform(double zoom, double panX, double panY)
    {
        var bounds = _videoSurface.Bounds;
        var fit = ReachVideoGeometry.CalculateFit(
            bounds.Width,
            bounds.Height,
            _videoWidth,
            _videoHeight,
            zoom,
            panX,
            panY);
        if (fit.Scale <= 0)
            return;

        _videoZoom = fit.Zoom;
        _videoScale.ScaleX = fit.Zoom;
        _videoScale.ScaleY = fit.Zoom;
        _videoTranslation.X = fit.PanX;
        _videoTranslation.Y = fit.PanY;
    }

    private void ResetVideoPanIfUnzoomed()
    {
        if (_videoZoom <= 1)
            ApplyVideoTransform(1, 0, 0);
    }

    private static double Distance(Point first, Point second)
    {
        var x = first.X - second.X;
        var y = first.Y - second.Y;
        return Math.Sqrt(x * x + y * y);
    }

    private static Point Midpoint(Point first, Point second) =>
        new((first.X + second.X) / 2, (first.Y + second.Y) / 2);

    private void OnVideoPointerWheel(object? sender, PointerWheelEventArgs args)
    {
        var delta = (int)Math.Round(args.Delta.Y * 120);
        if (delta == 0)
            return;

        QueueInput(() => _session.SendPointerWheelAsync(delta));
        args.Handled = true;
    }

    private void SendTextClicked(
        object? sender,
        RoutedEventArgs args)
    {
        SendRemoteText();
        _remoteTextInput.Focus();
    }

    private void KeyboardToggleClicked(
        object? sender,
        RoutedEventArgs args)
    {
        _keyboardMode = !_keyboardMode;
        _remoteTextInput.IsVisible = _keyboardMode;
        _sendText.IsVisible = _keyboardMode;
        _keyboardToggle.Content = _keyboardMode
            ? "Hide keyboard"
            : "Keyboard";
        if (_keyboardMode)
        {
            _remoteTextInput.Focus();
            _remoteTextInput.SelectAll();
            OnStatusChanged(
                "Keyboard mode active. Type, compose, then press Send.");
        }
        else
        {
            _videoSurface.Focus();
            OnStatusChanged("Remote surface focused.");
        }
    }

    private void ScrollModeClicked(
        object? sender,
        RoutedEventArgs args)
    {
        _scrollModeEnabled = !_scrollModeEnabled;
        _scrollMode.Content = _scrollModeEnabled
            ? "Scroll: on"
            : "Scroll";
        OnStatusChanged(
            _scrollModeEnabled
                ? "Two-finger scrolling is active."
                : "Two-finger pan and pinch are active.");
    }

    private void RemoteTextKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key != Key.Return)
            return;

        SendRemoteText();
        args.Handled = true;
    }

    private void SendRemoteText()
    {
        var text = _remoteTextInput.Text;
        if (string.IsNullOrEmpty(text))
            return;

        QueueInput(() => _session.SendTextInputAsync(text));
        _remoteTextInput.Clear();
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
        return TryGetRemotePoint(
            args.GetPosition(_videoSurface),
            out x,
            out y);
    }

    private bool TryGetRemotePoint(
        Point point,
        out double x,
        out double y)
    {
        x = 0;
        y = 0;
        if (_videoWidth <= 0 || _videoHeight <= 0)
            return false;

        var bounds = _videoSurface.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return false;

        var fit = ReachVideoGeometry.CalculateFit(
            bounds.Width,
            bounds.Height,
            _videoWidth,
            _videoHeight,
            _videoZoom,
            _videoTranslation.X,
            _videoTranslation.Y);
        return ReachVideoGeometry.TryMapPoint(
            fit,
            point.X,
            point.Y,
            _videoWidth,
            _videoHeight,
            _selectedDisplayLeft,
            _selectedDisplayTop,
            _selectedDisplayWidth,
            _selectedDisplayHeight,
            out x,
            out y);
    }

    private static string? GetPressedButton(
        PointerPointProperties properties,
        PointerType pointerType)
    {
        if (pointerType == PointerType.Touch)
            return "Left";
        if (properties.IsLeftButtonPressed)
            return "Left";
        if (properties.IsRightButtonPressed)
            return "Right";
        if (properties.IsMiddleButtonPressed)
            return "Middle";
        return null;
    }

    private static ReachVideoProfileKind ResolveInitialVideoProfileKind(
        string? endpoint) =>
        endpoint?.Contains("100.", StringComparison.Ordinal) == true
            || endpoint?.Contains(".ts.net", StringComparison.OrdinalIgnoreCase)
                == true
            ? ReachVideoProfileKind.Routed
            : ReachVideoProfileKind.Lan;

    private void QueueInput(Func<Task> input)
    {
        lock (_inputGate)
        {
            QueueInputLocked(input);
        }
    }

    private void QueueInputLocked(Func<Task> input)
    {
        _inputTail = _inputTail
            .ContinueWith(
                _ => SendInputAsync(input),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default)
            .Unwrap();
    }

    private void QueuePointerMove(double x, double y)
    {
        lock (_inputGate)
        {
            _pendingPointerMove = new Point(x, y);
            if (_pointerMoveQueued)
                return;

            _pointerMoveQueued = true;
            QueueInputLocked(SendLatestPointerMoveAsync);
        }
    }

    private async Task SendLatestPointerMoveAsync()
    {
        Point? point;
        lock (_inputGate)
        {
            point = _pendingPointerMove;
            _pendingPointerMove = null;
        }

        if (point is { } latest)
        {
            await SendInputAsync(() => _session.SendPointerMoveAsync(
                latest.X,
                latest.Y)).ConfigureAwait(false);
        }

        lock (_inputGate)
        {
            _pointerMoveQueued = false;
            if (_pendingPointerMove is not null)
            {
                _pointerMoveQueued = true;
                QueueInputLocked(SendLatestPointerMoveAsync);
            }
        }
    }

    private void QueuePointerButton(string button, bool isDown)
    {
        QueueInput(async () =>
        {
            Point? point;
            lock (_inputGate)
            {
                point = _pendingPointerMove;
                _pendingPointerMove = null;
            }

            if (point is { } latest)
            {
                await _session.SendPointerMoveAsync(
                    latest.X,
                    latest.Y).ConfigureAwait(false);
            }

            await _session.SendPointerButtonAsync(button, isDown)
                .ConfigureAwait(false);
        });
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

    private static IReadOnlyList<string> LoadRememberedEndpoints()
    {
        try
        {
            var path = GetEndpointHistoryPath();
            return File.Exists(path)
                ? File.ReadAllLines(path)
                    .Where(static endpoint => !string.IsNullOrWhiteSpace(endpoint))
                    .Take(8)
                    .ToArray()
                : [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static void RememberEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return;

        try
        {
            var endpoints = LoadRememberedEndpoints()
                .Where(item => !string.Equals(
                    item,
                    endpoint,
                    StringComparison.OrdinalIgnoreCase))
                .Prepend(endpoint)
                .Take(8)
                .ToArray();
            var path = GetEndpointHistoryPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllLines(path, endpoints);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string GetEndpointHistoryPath() =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "Reach",
            "endpoints.txt");

    private static ReachPlatform ResolvePlatform() =>
        OperatingSystem.IsAndroid()
            ? ReachPlatform.Android
            : OperatingSystem.IsLinux()
                ? ReachPlatform.Linux
                : ReachPlatform.Windows;
}
