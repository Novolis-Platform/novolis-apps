using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Novolis.Avalonia.Video;
using Novolis.Reach.Client;
using Novolis.Video;

namespace Novolis.Avalonia.Reach;

/// <summary>Shared Avalonia client surface for endpoint selection and session status.</summary>
public sealed class ReachClientView : UserControl
{
    internal readonly ReachClientSession _session;
    internal readonly IReachVideoPresenter _presenter;
    internal readonly IReachAudioPresenter _audioPresenter;
    internal readonly ReachClipboardSynchronizer _clipboard;
    internal readonly ReachPressedKeyTracker _pressedKeys = new();
    internal readonly Dictionary<int, Point> _touchPoints = [];
    internal readonly ScaleTransform _videoScale = new(1, 1);
    internal readonly TranslateTransform _videoTranslation = new();
    internal readonly object _inputGate = new();
    internal readonly object _frameGate = new();
    internal readonly Dictionary<string, string> _discoveredHostEndpoints = [];
    internal IReadOnlyList<ReachDisplay> _displays = Array.Empty<ReachDisplay>();
    internal TextBox _endpoint = null!;
    internal TextBlock _status = null!;
    internal TextBlock _capabilities = null!;
    internal TextBlock _sessionPhase = null!;
    internal TextBlock _performanceStatus = null!;
    internal ListBox _discoveredHosts = null!;
    internal Button _discover = null!;
    internal Button _connect = null!;
    internal ComboBox _displaySelector = null!;
    internal Button _keyboardToggle = null!;
    internal Button _fitToScreen = null!;
    internal Button _resetZoom = null!;
    internal Button _scrollMode = null!;
    internal StackPanel _sessionToolbar = null!;
    internal VideoSurface _videoImage = null!;
    internal Border _videoSurface = null!;
    internal TextBox _remoteTextInput = null!;
    internal Button _sendText = null!;
    internal Task _inputTail = Task.CompletedTask;
    internal Point? _pendingPointerMove;
    internal bool _pointerMoveQueued;
    internal int _videoWidth;
    internal int _videoHeight;
    internal int _selectedDisplayLeft;
    internal int _selectedDisplayTop;
    internal int _selectedDisplayWidth;
    internal int _selectedDisplayHeight;
    internal RawVideoFrame? _pendingFrame;
    internal bool _frameUpdateScheduled;
    internal bool _platformVideoConfigured;
    internal bool _touchGestureActive;
    internal bool _touchRemoteButtonDown;
    internal bool _touchLongPressFired;
    internal Point _touchPressPoint;
    internal CancellationTokenSource? _touchLongPressCancellation;
    internal double _gestureStartDistance;
    internal double _gestureStartZoom;
    internal double _gestureStartPanX;
    internal double _gestureStartPanY;
    internal Point _gestureStartCenter;
    internal Point _lastGestureCenter;
    internal double _videoZoom = 1;
    internal bool _discoveryActive;
    internal bool _streamStatusShown;
    internal bool _keyboardMode;
    internal bool _scrollModeEnabled;
    internal string _endpointValue = string.Empty;
    internal int _statusPriority;
    internal CancellationTokenSource? _reconnectCancellation;
    internal CancellationTokenSource? _connectCancellation;
    internal ReachVideoProfileController? _videoProfileController;
    internal ReachVideoProfile? _activeVideoProfile;
    internal bool _sessionEnded;
    internal bool _displaySelectionUpdating;
    internal string? _selectedDisplayId;

    /// <summary>Creates the shared client surface.</summary>
    public ReachClientView(
        ReachClientSession session,
        IReachVideoPresenter? presenter = null,
        IReachAudioPresenter? audioPresenter = null,
        IReachClipboardBridge? clipboardBridge = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Connection = new ReachClientViewConnection(this);
        Status = new ReachClientViewStatus(this);
        Frames = new ReachClientViewFrames(this);
        Pointer = new ReachClientViewPointer(this);
        Keyboard = new ReachClientViewKeyboard(this);
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
        _presenter = presenter ?? new NullReachVideoPresenter();
        _session.VideoFrameReceived += _presenter.Present;
        _presenter.FrameDecoded += OnFrameDecoded;
        if (_presenter is IReachVideoPerformanceSource performanceSource)
            performanceSource.DecodeCompleted += _session.RecordDecodedFrame;
        if (_presenter is IReachVideoDropSource dropSource)
            dropSource.FrameDropped += _session.RecordDroppedFrame;
        if (_presenter is IReachKeyFrameRequester keyFrameRequester)
            keyFrameRequester.KeyFrameRequested += OnKeyFrameRequested;
        _audioPresenter = audioPresenter ?? new NullReachAudioPresenter();
        _session.AudioFrameReceived += _audioPresenter.Present;
        _clipboard = new ReachClipboardSynchronizer(
            _session,
            clipboardBridge ?? new AvaloniaReachClipboardBridge(this));
        _clipboard.StatusChanged += OnStatusChanged;
        Content = ReachClientChrome.Build(this);
        AttachedToVisualTree += (_, _) => _ = _clipboard.StartAsync();
        DetachedFromVisualTree += (_, _) => _ = _clipboard.StopAsync();
        _ = Connection.DiscoverHostsAsync();
    }

    internal ReachClientViewConnection Connection { get; }
    internal ReachClientViewStatus Status { get; }
    internal ReachClientViewFrames Frames { get; }
    internal ReachClientViewPointer Pointer { get; }
    internal ReachClientViewKeyboard Keyboard { get; }

    internal void EndpointTextChanged(object? sender, TextChangedEventArgs args) =>
        Connection.EndpointTextChanged(sender, args);

    internal void DiscoveredHostSelected(object? sender, SelectionChangedEventArgs args) =>
        Connection.DiscoveredHostSelected(sender, args);

    internal void DisplaySelectionChanged(object? sender, SelectionChangedEventArgs args) =>
        Frames.DisplaySelectionChanged(sender, args);

    internal void DiscoverClicked(object? sender, RoutedEventArgs args) =>
        Connection.DiscoverClicked(sender, args);

    internal Task DiscoverHostsAsync() => Connection.DiscoverHostsAsync();

    internal void ConnectClicked(object? sender, RoutedEventArgs args) =>
        Connection.ConnectClicked(sender, args);

    internal Task<bool> ConnectToEndpointAsync() => Connection.ConnectToEndpointAsync();

    internal Task<bool> ReconnectToEndpointAsync(
        CancellationToken cancellationToken = default,
        bool cancelExistingReconnect = true) =>
        Connection.ReconnectToEndpointAsync(cancellationToken, cancelExistingReconnect);

    internal void OnStatusChanged(string status) => Status.OnStatusChanged(status);

    internal void ResetStatusPriority() => Status.ResetStatusPriority();

    internal void OnPhaseChanged(ReachConnectionPhase phase) => Status.OnPhaseChanged(phase);

    internal void OnPerformanceChanged(ReachPerformanceSnapshot snapshot) =>
        Status.OnPerformanceChanged(snapshot);

    internal void SetConnectedStatus(bool reconnected = false) =>
        Status.SetConnectedStatus(reconnected);

    internal static int GetStatusPriority(string status) =>
        ReachClientViewStatus.GetStatusPriority(status);

    internal void OnSessionStateChanged(ReachClientConnectionState state) =>
        Status.OnSessionStateChanged(state);

    internal void OnSessionEnded(string reason) => Status.OnSessionEnded(reason);

    internal void OnSharingStateChanged(ReachSharingState state) =>
        Status.OnSharingStateChanged(state);

    internal void OnMediaConnectionLost() => Status.OnMediaConnectionLost();

    internal void OnConnectionLost() => Status.OnConnectionLost();

    internal Task RecoverMediaAsync() => Status.RecoverMediaAsync();

    internal void BeginReconnectLoop() => Status.BeginReconnectLoop();

    internal void CancelReconnect() => Status.CancelReconnect();

    internal void OnVideoStreamReset(ReachVideoStreamReset reset) =>
        Frames.OnVideoStreamReset(reset);

    internal void UpdateConnectionControls() => Frames.UpdateConnectionControls();

    internal void OnFrameDecoded(RawVideoFrame frame) => Frames.OnFrameDecoded(frame);

    internal void ClearVideoFrame() => Frames.ClearVideoFrame();

    internal void OnKeyFrameRequested() => Frames.OnKeyFrameRequested();

    internal void OnDisplayTopology(ReachDisplayTopology topology) =>
        Frames.OnDisplayTopology(topology);

    internal void OnVideoStreamStarted(ReachVideoStreamStart stream) =>
        Frames.OnVideoStreamStarted(stream);

    internal Task RequestRemoteClipboardAsync(
        CancellationToken cancellationToken = default) =>
        _clipboard.RequestRemoteClipboardAsync(cancellationToken);

    internal void OnVideoPointerPressed(object? sender, PointerPressedEventArgs args) =>
        Pointer.OnVideoPointerPressed(sender, args);

    internal void OnVideoPointerMoved(object? sender, PointerEventArgs args) =>
        Pointer.OnVideoPointerMoved(sender, args);

    internal void OnVideoPointerReleased(object? sender, PointerReleasedEventArgs args) =>
        Pointer.OnVideoPointerReleased(sender, args);

    internal void OnVideoPointerWheel(object? sender, PointerWheelEventArgs args) =>
        Pointer.OnVideoPointerWheel(sender, args);

    internal void CancelTouchLongPress() => Pointer.CancelTouchLongPress();

    internal void StartTouchLongPress() => Pointer.StartTouchLongPress();

    internal void BeginTouchGesture() => Pointer.BeginTouchGesture();

    internal void UpdateTouchGesture() => Pointer.UpdateTouchGesture();

    internal void QueueTouchTap(double x, double y) => Pointer.QueueTouchTap(x, y);

    internal void ApplyVideoTransform(double zoom, double panX, double panY) =>
        Pointer.ApplyVideoTransform(zoom, panX, panY);

    internal void ResetVideoPanIfUnzoomed() => Pointer.ResetVideoPanIfUnzoomed();

    internal double Distance(Point first, Point second) =>
        ReachClientViewPointer.Distance(first, second);

    internal Point Midpoint(Point first, Point second) =>
        ReachClientViewPointer.Midpoint(first, second);

    internal void SendTextClicked(object? sender, RoutedEventArgs args) =>
        Keyboard.SendTextClicked(sender, args);

    internal void KeyboardToggleClicked(object? sender, RoutedEventArgs args) =>
        Keyboard.KeyboardToggleClicked(sender, args);

    internal void ScrollModeClicked(object? sender, RoutedEventArgs args) =>
        Keyboard.ScrollModeClicked(sender, args);

    internal void RemoteTextKeyDown(object? sender, KeyEventArgs args) =>
        Keyboard.RemoteTextKeyDown(sender, args);

    internal void OnVideoKeyDown(object? sender, KeyEventArgs args) =>
        Keyboard.OnVideoKeyDown(sender, args);

    internal void OnVideoSurfaceLostFocus(
        object? sender,
        RoutedEventArgs args) =>
        Keyboard.ReleasePressedKeys();

    internal void OnVideoKeyUp(object? sender, KeyEventArgs args) =>
        Keyboard.OnVideoKeyUp(sender, args);

    internal void OnVideoTextInput(object? sender, TextInputEventArgs args) =>
        Keyboard.OnVideoTextInput(sender, args);

    internal bool TryGetRemotePoint(PointerEventArgs args, out double x, out double y) =>
        Keyboard.TryGetRemotePoint(args, out x, out y);

    internal bool TryGetRemotePoint(Point point, out double x, out double y) =>
        Keyboard.TryGetRemotePoint(point, out x, out y);

    internal void QueueInput(Func<Task> input) => Keyboard.QueueInput(input);

    internal void ReleasePressedKeys() => Keyboard.ReleasePressedKeys();

    internal void QueuePointerMove(double x, double y) => Keyboard.QueuePointerMove(x, y);

    internal void QueuePointerButton(string button, bool isDown) =>
        Keyboard.QueuePointerButton(button, isDown);

    internal static ReachPlatform ResolvePlatform() =>
        OperatingSystem.IsAndroid()
            ? ReachPlatform.Android
            : OperatingSystem.IsLinux()
                ? ReachPlatform.Linux
                : ReachPlatform.Windows;
}
