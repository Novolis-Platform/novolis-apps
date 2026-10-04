using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.Video;

namespace Novolis.Avalonia.Reach;

internal static class ReachClientChrome
{
    internal static Control Build(ReachClientView view)
    {
        view._endpoint = new TextBox
        {
            Name = "ReachEndpoint",
            Text = ReachClientEndpointStore.ResolveDefault(),
            PlaceholderText = "Searching for Reach hosts...",
            Width = OperatingSystem.IsAndroid() ? double.NaN : 260,
            MinWidth = OperatingSystem.IsAndroid() ? 0 : 260,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        view._endpoint.TextChanged += view.EndpointTextChanged;
        if (ReachClientEndpointStore.Load().FirstOrDefault() is { } remembered)
            view._endpoint.Text = remembered;
        view._endpointValue = view._endpoint.Text ?? string.Empty;
        view._discover = new Button
        {
            Name = "ReachDiscover",
            Content = "Discover",
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 0,
            Padding = new Thickness(8, 4),
        };
        view._discover.Click += view.DiscoverClicked;
        view._connect = new Button
        {
            Name = "ReachConnect",
            Content = "Connect",
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = false,
            MinWidth = 0,
            Padding = new Thickness(8, 4),
        };
        view._connect.Click += view.ConnectClicked;
        view._keyboardToggle = new Button
        {
            Name = "ReachKeyboard",
            Content = "Keyboard",
            IsVisible = OperatingSystem.IsAndroid(),
            MinWidth = 0,
        };
        view._keyboardToggle.Click += view.KeyboardToggleClicked;
        view._fitToScreen = new Button
        {
            Name = "ReachFitToScreen",
            Content = "Fit",
            IsVisible = OperatingSystem.IsAndroid(),
            MinWidth = 0,
        };
        view._fitToScreen.Click += (_, _) => view.ApplyVideoTransform(1, 0, 0);
        view._resetZoom = new Button
        {
            Name = "ReachResetZoom",
            Content = "Reset zoom",
            IsVisible = OperatingSystem.IsAndroid(),
            MinWidth = 0,
        };
        view._resetZoom.Click += (_, _) => view.ApplyVideoTransform(1, 0, 0);
        view._scrollMode = new Button
        {
            Name = "ReachScrollMode",
            Content = "Scroll",
            IsVisible = OperatingSystem.IsAndroid(),
            MinWidth = 0,
        };
        view._scrollMode.Click += view.ScrollModeClicked;
        view._sessionToolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            IsVisible = OperatingSystem.IsAndroid(),
            Children =
            {
                view._keyboardToggle,
                view._fitToScreen,
                view._resetZoom,
                view._scrollMode,
            },
        };
        view._status = new TextBlock
        {
            Text = "Searching for Reach hosts on LAN and Tailscale...",
            TextWrapping = TextWrapping.Wrap,
        };
        view._capabilities = new TextBlock
        {
            Text = "Capabilities: not negotiated",
            TextWrapping = TextWrapping.Wrap,
        };
        view._sessionPhase = new TextBlock
        {
            Text = "Phase: Disconnected",
            TextWrapping = TextWrapping.Wrap,
        };
        view._performanceStatus = new TextBlock
        {
            Text = "Performance: waiting for frames",
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        };
        view._discoveredHosts = new ListBox
        {
            Name = "ReachDiscoveredHosts",
            Height = 72,
            IsVisible = false,
            SelectionMode = SelectionMode.Single,
        };
        view._discoveredHosts.SelectionChanged += view.DiscoveredHostSelected;
        view._videoImage = new VideoSurface
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = true,
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            RenderTransform = new TransformGroup
            {
                Children = { view._videoScale, view._videoTranslation },
            },
        };
        view._videoSurface = new Border
        {
            Name = "ReachVideoSurface",
            Background = Brushes.Black,
            Focusable = true,
            IsHitTestVisible = true,
            MinHeight = OperatingSystem.IsAndroid() ? 220 : 360,
            ClipToBounds = true,
            Child = view._videoImage,
        };
        view._videoSurface.KeyDown += view.OnVideoKeyDown;
        view._videoSurface.KeyUp += view.OnVideoKeyUp;
        view._videoSurface.AddHandler(
            InputElement.TextInputEvent,
            view.OnVideoTextInput,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        view._videoSurface.AddHandler(
            InputElement.PointerPressedEvent,
            view.OnVideoPointerPressed,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        view._videoSurface.AddHandler(
            InputElement.PointerMovedEvent,
            view.OnVideoPointerMoved,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        view._videoSurface.AddHandler(
            InputElement.PointerReleasedEvent,
            view.OnVideoPointerReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        view._videoSurface.PointerWheelChanged += view.OnVideoPointerWheel;
        view._remoteTextInput = new TextBox
        {
            Name = "ReachRemoteTextInput",
            PlaceholderText = "Type to send to remote session",
            Width = double.NaN,
            MinWidth = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsVisible = false,
        };
        view._sendText = new Button
        {
            Name = "ReachSendText",
            Content = "Send",
            IsVisible = false,
        };
        view._sendText.Click += view.SendTextClicked;
        view._remoteTextInput.KeyDown += view.RemoteTextKeyDown;
        var remoteTextRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
            IsVisible = OperatingSystem.IsAndroid(),
            Children = { view._remoteTextInput, view._sendText },
        };
        Grid.SetColumn(view._sendText, 1);
        var endpointRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            ColumnSpacing = 8,
            Children = { view._endpoint, view._discover, view._connect },
        };
        Grid.SetColumn(view._discover, 1);
        Grid.SetColumn(view._connect, 2);
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions(
                "Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto,*"),
            Margin = new Thickness(OperatingSystem.IsAndroid() ? 12 : 24),
            RowSpacing = 12,
            Children =
            {
                endpointRow,
                view._discoveredHosts,
                view._status,
                view._capabilities,
                view._sessionPhase,
                view._performanceStatus,
                view._sessionToolbar,
                remoteTextRow,
                view._videoSurface,
            },
        };
        Grid.SetRow(view._discoveredHosts, 1);
        Grid.SetRow(view._status, 2);
        Grid.SetRow(view._capabilities, 3);
        Grid.SetRow(view._sessionPhase, 4);
        Grid.SetRow(view._performanceStatus, 5);
        Grid.SetRow(view._sessionToolbar, 6);
        Grid.SetRow(remoteTextRow, 7);
        Grid.SetRow(view._videoSurface, 8);
        return root;
    }
}
