using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Avalonia.Video;
using Ngp = Novolis.Avalonia.GraphicalProfile.GraphicalProfile;
using NgpBinding = Novolis.Avalonia.GraphicalProfile.GraphicalProfileBinding;

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
        view._endpoint.SetValue(
            AutomationProperties.AutomationIdProperty,
            "ReachEndpoint");
        NgpBinding.Bind(
            view._endpoint,
            TextBox.BackgroundProperty,
            Ngp.RaisedResourceKey);
        NgpBinding.Bind(
            view._endpoint,
            TextBox.ForegroundProperty,
            Ngp.TextResourceKey);
        NgpBinding.Bind(
            view._endpoint,
            TextBox.BorderBrushProperty,
            Ngp.BorderResourceKey);
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
        view._discover.Classes.Add("nav-button");
        view._discover.SetValue(
            AutomationProperties.AutomationIdProperty,
            "ReachDiscover");
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
        view._connect.Classes.Add("primary-button");
        view._connect.SetValue(
            AutomationProperties.AutomationIdProperty,
            "ReachConnect");
        view._connect.Click += view.ConnectClicked;
        view._keyboardToggle = new Button
        {
            Name = "ReachKeyboard",
            Content = "Keyboard",
            IsVisible = OperatingSystem.IsAndroid(),
            MinWidth = 0,
        };
        view._keyboardToggle.Classes.Add("nav-button");
        view._keyboardToggle.SetValue(
            AutomationProperties.AutomationIdProperty,
            "ReachKeyboard");
        view._keyboardToggle.Click += view.KeyboardToggleClicked;
        view._fitToScreen = new Button
        {
            Name = "ReachFitToScreen",
            Content = "Fit",
            IsVisible = OperatingSystem.IsAndroid(),
            MinWidth = 0,
        };
        view._fitToScreen.Classes.Add("nav-button");
        view._fitToScreen.SetValue(
            AutomationProperties.AutomationIdProperty,
            "ReachFitToScreen");
        view._fitToScreen.Click += (_, _) => view.FitVideoToScreen();
        view._resetZoom = new Button
        {
            Name = "ReachResetZoom",
            Content = "Reset zoom",
            IsVisible = OperatingSystem.IsAndroid(),
            MinWidth = 0,
        };
        view._resetZoom.Classes.Add("nav-button");
        view._resetZoom.SetValue(
            AutomationProperties.AutomationIdProperty,
            "ReachResetZoom");
        view._resetZoom.Click += (_, _) => view.ResetVideoZoom();
        view._scrollMode = new Button
        {
            Name = "ReachScrollMode",
            Content = "Scroll",
            IsVisible = OperatingSystem.IsAndroid(),
            MinWidth = 0,
        };
        view._scrollMode.Classes.Add("nav-button");
        view._scrollMode.SetValue(
            AutomationProperties.AutomationIdProperty,
            "ReachScrollMode");
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
        view._status.SetValue(
            AutomationProperties.AutomationIdProperty,
            "ReachStatus");
        view._status.Classes.Add("body-copy");
        view._capabilities = new TextBlock
        {
            Text = "Capabilities: not negotiated",
            TextWrapping = TextWrapping.Wrap,
        };
        view._capabilities.SetValue(
            AutomationProperties.AutomationIdProperty,
            "ReachCapabilities");
        view._capabilities.Classes.Add("body-copy");
        view._sessionPhase = new TextBlock
        {
            Text = "Phase: Disconnected",
            TextWrapping = TextWrapping.Wrap,
        };
        view._sessionPhase.SetValue(
            AutomationProperties.AutomationIdProperty,
            "ReachSessionPhase");
        view._sessionPhase.Classes.Add("body-copy");
        view._performanceStatus = new TextBlock
        {
            Text = "Performance: waiting for frames",
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        };
        view._performanceStatus.SetValue(
            AutomationProperties.AutomationIdProperty,
            "ReachPerformance");
        view._performanceStatus.Classes.Add("body-copy");
        view._discoveredHosts = new ListBox
        {
            Name = "ReachDiscoveredHosts",
            Height = 72,
            IsVisible = false,
            SelectionMode = SelectionMode.Single,
        };
        view._discoveredHosts.SelectionChanged += view.DiscoveredHostSelected;
        view._displaySelector = new ComboBox
        {
            Name = "ReachDisplaySelector",
            IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = OperatingSystem.IsAndroid() ? 0 : 260,
        };
        view._displaySelector.SetValue(
            AutomationProperties.AutomationIdProperty,
            "ReachDisplaySelector");
        NgpBinding.Bind(
            view._displaySelector,
            ComboBox.BackgroundProperty,
            Ngp.RaisedResourceKey);
        NgpBinding.Bind(
            view._displaySelector,
            ComboBox.ForegroundProperty,
            Ngp.TextResourceKey);
        NgpBinding.Bind(
            view._displaySelector,
            ComboBox.BorderBrushProperty,
            Ngp.BorderResourceKey);
        view._displaySelector.SelectionChanged += view.DisplaySelectionChanged;
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
        view._keyboardInput = new TextBox
        {
            Name = "ReachKeyboardInput",
            Width = 1,
            Height = 1,
            MinWidth = 0,
            MinHeight = 0,
            MaxWidth = 1,
            MaxHeight = 1,
            Opacity = 0,
            IsVisible = OperatingSystem.IsAndroid(),
            IsHitTestVisible = false,
            Focusable = true,
            IsTabStop = false,
            AcceptsReturn = true,
            Padding = new Thickness(0),
            TextWrapping = TextWrapping.NoWrap,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        view._keyboardInput.SetValue(
            AutomationProperties.AutomationIdProperty,
            "ReachRemoteTextInput");
        view._keyboardInput.KeyDown += view.OnKeyboardInputKeyDown;
        view._keyboardInput.KeyUp += view.OnKeyboardInputKeyUp;
        view._keyboardInput.AddHandler(
            InputElement.TextInputEvent,
            view.OnKeyboardInputTextInput,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        var videoContent = new Grid
        {
            Children =
            {
                view._videoImage,
                view._keyboardInput,
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
            Child = videoContent,
        };
        view._videoSurface.SetValue(
            AutomationProperties.AutomationIdProperty,
            "ReachVideoSurface");
        view._videoSurface.LostFocus += view.OnVideoSurfaceLostFocus;
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

        view._sessionHeader = new TextBlock
        {
            Text = "Not connected",
            FontSize = 17,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        NgpBinding.Bind(
            view._sessionHeader,
            TextBlock.ForegroundProperty,
            Ngp.TextResourceKey);
        view._sessionHeader.SetValue(
            AutomationProperties.AutomationIdProperty,
            "ReachSessionHeader");

        view._disconnect = new Button
        {
            Name = "ReachDisconnect",
            Content = "Cancel",
            MinWidth = 92,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            IsEnabled = false,
        };
        view._disconnect.Classes.Add("nav-button");
        view._disconnect.SetValue(
            AutomationProperties.AutomationIdProperty,
            "ReachDisconnect");
        view._disconnect.Click += view.DisconnectClicked;

        view._fullscreen = new Button
        {
            Name = "ReachFullscreen",
            Content = "Fullscreen",
            MinWidth = 92,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            IsVisible = OperatingSystem.IsWindows(),
        };
        view._fullscreen.Classes.Add("nav-button");
        view._fullscreen.SetValue(
            AutomationProperties.AutomationIdProperty,
            "ReachFullscreen");
        view._fullscreen.Click += view.FullscreenClicked;

        view._diagnosticsToggle = new Button
        {
            Name = "ReachDiagnostics",
            Content = "Details",
            MinWidth = 76,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        view._diagnosticsToggle.Classes.Add("nav-button");
        view._diagnosticsToggle.SetValue(
            AutomationProperties.AutomationIdProperty,
            "ReachDiagnostics");
        view._diagnosticsToggle.Click += view.ToggleDiagnosticsClicked;

        var connectionLabel = new TextBlock
        {
            Text = "Connect to a trusted LAN or Tailscale host",
            TextWrapping = TextWrapping.Wrap,
        };
        connectionLabel.Classes.Add("body-copy");
        var endpointRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            ColumnSpacing = 8,
            Children = { view._endpoint, view._discover, view._connect },
        };
        Grid.SetColumn(view._discover, 1);
        Grid.SetColumn(view._connect, 2);

        view._launcherPanel = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                connectionLabel,
                endpointRow,
                view._discoveredHosts,
            },
        };
        view._launcherPanel.SetValue(
            AutomationProperties.AutomationIdProperty,
            "ReachLauncher");

        var sessionActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children =
            {
                view._disconnect,
                view._diagnosticsToggle,
                view._fullscreen,
            },
        };

        var sessionHeader = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 12,
            Children =
            {
                view._sessionHeader,
                sessionActions,
            },
        };
        Grid.SetColumn(sessionActions, 1);

        var sessionInfo = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                sessionHeader,
                view._sessionPhase,
                view._capabilities,
                view._performanceStatus,
            },
        };

        view._sessionPanel = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"),
            RowSpacing = 8,
            Children =
            {
                sessionInfo,
                view._displaySelector,
                view._videoSurface,
                view._sessionToolbar,
            },
        };
        Grid.SetRow(sessionInfo, 0);
        Grid.SetRow(view._displaySelector, 1);
        Grid.SetRow(view._videoSurface, 2);
        Grid.SetRow(view._sessionToolbar, 3);
        view._sessionPanel.SetValue(
            AutomationProperties.AutomationIdProperty,
            "ReachSession");

        view._videoSurface.PointerMoved += (_, _) => view.ShowSessionToolbar();
        view._sessionToolbar.PointerEntered += (_, _) => view.StopToolbarAutoHide();
        view._sessionToolbar.PointerExited += (_, _) => view.ShowSessionToolbar();

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            Margin = new Thickness(OperatingSystem.IsAndroid() ? 12 : 24),
            RowSpacing = 12,
            Children =
            {
                ReachBranding.BuildHeader(),
                view._status,
                view._launcherPanel,
                view._sessionPanel,
            },
        };
        NgpBinding.Bind(
            root,
            Grid.BackgroundProperty,
            Ngp.BackgroundResourceKey);
        Grid.SetRow(view._status, 1);
        Grid.SetRow(view._launcherPanel, 2);
        Grid.SetRow(view._sessionPanel, 2);
        view.SetSessionSurface(false);
        return root;
    }
}
