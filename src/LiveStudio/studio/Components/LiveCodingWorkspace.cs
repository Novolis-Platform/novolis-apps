using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Audio.Live;

namespace LiveStudio.Components;

internal sealed class LiveCodingWorkspace : Grid
{
    private static readonly SolidColorBrush WorkspaceBackground = new(Color.Parse("#0F172A"));
    private static readonly SolidColorBrush ToolbarBackground = new(Color.Parse("#111827"));
    private static readonly SolidColorBrush ToolbarBorder = new(Color.Parse("#243047"));
    private static readonly SolidColorBrush TextBrush = new(Color.Parse("#E2E8F0"));
    private static readonly SolidColorBrush MutedBrush = new(Color.Parse("#94A3B8"));

    private readonly LiveCodeEditor _editor = new();
    private readonly LiveStudioDashboard _dashboard = new();
    private readonly TextBlock _launcherStatus = new();
    private readonly ComboBox _swapPolicy = new();
    private readonly Button _compileButton = new() { Content = "Compile to host" };
    private readonly Button _loadDefaultButton = new() { Content = "Reset template" };

    public LiveCodingWorkspace()
    {
        Background = WorkspaceBackground;
        RowDefinitions = new RowDefinitions("Auto,*");
        ColumnDefinitions = new ColumnDefinitions("3*,2*");

        var toolbar = BuildToolbar();
        Grid.SetRow(toolbar, 0);
        Grid.SetColumnSpan(toolbar, 2);
        Children.Add(toolbar);

        var editorHost = new Border
        {
            Margin = new Thickness(16, 0, 8, 16),
            Child = _editor,
        };
        Grid.SetRow(editorHost, 1);
        Grid.SetColumn(editorHost, 0);
        Children.Add(editorHost);

        var dashboardHost = new Border
        {
            Margin = new Thickness(8, 0, 16, 16),
            Child = _dashboard,
        };
        Grid.SetRow(dashboardHost, 1);
        Grid.SetColumn(dashboardHost, 1);
        Children.Add(dashboardHost);

        _editor.Text = LiveCodeTemplates.DefaultSource;
        _compileButton.Click += (_, _) => CompileRequested?.Invoke(this, EventArgs.Empty);
        _loadDefaultButton.Click += (_, _) => _editor.Text = LiveCodeTemplates.DefaultSource;
        _editor.CompileRequested += (_, _) => CompileRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? CompileRequested;

    public string SourceText => _editor.Text;

    public SwapPolicy SelectedSwapPolicy =>
        _swapPolicy.SelectedItem is SwapPolicy policy ? policy : SwapPolicy.Immediately;

    public void Bind(LiveStudioState state)
    {
        _launcherStatus.Text = state.LauncherStatus;
        _launcherStatus.Foreground = state.HasFatalLauncherError
            ? new SolidColorBrush(Color.Parse("#FCA5A5"))
            : MutedBrush;

        _dashboard.Bind(state);
    }

    private Control BuildToolbar()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
        };

        var title = BuildTitleCluster();
        var actions = BuildActionCluster();
        Grid.SetColumn(title, 0);
        Grid.SetColumn(actions, 2);
        grid.Children.Add(title);
        grid.Children.Add(actions);

        return new Border
        {
            Background = ToolbarBackground,
            BorderBrush = ToolbarBorder,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 12),
            Child = grid,
        };
    }

    private Control BuildTitleCluster()
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock
        {
            Text = "Live coding",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = TextBrush,
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Edit on the left, compile to the host on the right. Ctrl+Enter compiles immediately.",
            Foreground = MutedBrush,
            FontSize = 12,
        });
        panel.Children.Add(_launcherStatus);
        return panel;
    }

    private Control BuildActionCluster()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _swapPolicy.ItemsSource = new[]
        {
            SwapPolicy.Immediately,
            SwapPolicy.NextBeat,
            SwapPolicy.NextPhrase,
        };
        _swapPolicy.SelectedIndex = 0;
        _swapPolicy.MinWidth = 150;

        StyleActionButton(_compileButton);
        StyleActionButton(_loadDefaultButton);

        panel.Children.Add(new TextBlock
        {
            Text = "Swap",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = MutedBrush,
            Margin = new Thickness(0, 0, 4, 0),
        });
        panel.Children.Add(_swapPolicy);
        panel.Children.Add(_compileButton);
        panel.Children.Add(_loadDefaultButton);
        return panel;
    }

    private static void StyleActionButton(Button button)
    {
        button.Padding = new Thickness(14, 8);
        button.CornerRadius = new CornerRadius(8);
        button.Background = new SolidColorBrush(Color.Parse("#1D4ED8"));
        button.Foreground = Brushes.White;
    }
}
