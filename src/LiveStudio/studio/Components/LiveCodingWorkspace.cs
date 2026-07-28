using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
    private readonly Button _compileButton = new() { Content = "Compile (F5)" };
    private readonly Button _demoButton = new() { Content = "Replay demo" };
    private readonly Button _loadDefaultButton = new() { Content = "Reset buffer" };

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
        _demoButton.Click += (_, _) => DemoRequested?.Invoke(this, EventArgs.Empty);
        _loadDefaultButton.Click += (_, _) => _editor.Text = LiveCodeTemplates.DefaultSource;
        _editor.CompileRequested += (_, _) => CompileRequested?.Invoke(this, EventArgs.Empty);
        _dashboard.PresetSelected += preset => PresetSelected?.Invoke(preset);

        KeyDown += OnWorkspaceKeyDown;
        Focusable = true;
    }

    public event EventHandler? CompileRequested;
    public event EventHandler? DemoRequested;
    public event Action<LiveProgramPreset>? PresetSelected;

    public string SourceText => _editor.Text;

    public SwapPolicy SelectedSwapPolicy =>
        _swapPolicy.SelectedItem is SwapPolicy policy ? policy : SwapPolicy.Immediately;

    public void Bind(LiveStudioState state)
    {
        _launcherStatus.Text = state.LauncherStatus;
        _launcherStatus.Foreground = state.HasFatalLauncherError
            ? new SolidColorBrush(Color.Parse("#FCA5A5"))
            : MutedBrush;

        _demoButton.Content = state.DemoSequenceRunning ? "Demo playing…" : "Replay demo";
        _demoButton.IsEnabled = state.IsHostConnected && !state.DemoSequenceRunning && !state.HasFatalLauncherError;
        _compileButton.IsEnabled = state.IsHostConnected && !state.HasFatalLauncherError;

        _dashboard.Bind(state);
    }

    private void OnWorkspaceKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5)
        {
            CompileRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
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
            Text = "Presets play full tracks. Editor is Note.Play only — F5 compiles when connected.",
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

        StyleActionButton(_compileButton, primary: true);
        StyleActionButton(_demoButton, primary: false);
        StyleActionButton(_loadDefaultButton, primary: false);

        panel.Children.Add(new TextBlock
        {
            Text = "Swap",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = MutedBrush,
            Margin = new Thickness(0, 0, 4, 0),
        });
        panel.Children.Add(_swapPolicy);
        panel.Children.Add(_compileButton);
        panel.Children.Add(_demoButton);
        panel.Children.Add(_loadDefaultButton);
        return panel;
    }

    private static void StyleActionButton(Button button, bool primary)
    {
        button.Padding = new Thickness(14, 8);
        button.CornerRadius = new CornerRadius(8);
        button.Background = new SolidColorBrush(Color.Parse(primary ? "#1D4ED8" : "#334155"));
        button.Foreground = Brushes.White;
    }
}
