using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using LiveStudio.Components.Live;
using Novolis.Audio.Live;

namespace LiveStudio.Components;

internal sealed class LiveCodingWorkspace : Grid
{
    static readonly SolidColorBrush WorkspaceBackground = new(Color.Parse("#0F172A"));
    static readonly SolidColorBrush ToolbarBackground = new(Color.Parse("#111827"));
    static readonly SolidColorBrush ToolbarBorder = new(Color.Parse("#243047"));
    static readonly SolidColorBrush TextBrush = new(Color.Parse("#E2E8F0"));
    static readonly SolidColorBrush MutedBrush = new(Color.Parse("#94A3B8"));

    readonly LiveCodeEditorControl _editor = new();
    readonly LiveStudioDashboard _dashboard = new();
    readonly TextBlock _launcherStatus = new();
    readonly ComboBox _swapPolicy = new();
    readonly Button _compileButton = new() { Content = "Compile (F5)" };
    readonly Button _demoButton = new() { Content = "Replay demo" };
    readonly Button _loadDefaultButton = new() { Content = "Reset buffer" };
    readonly Button _graphWindowButton = new() { Content = "Graph window" };
    readonly Button _pianoWindowButton = new() { Content = "Piano roll" };
    readonly Button _interpWindowButton = new() { Content = "Interpretation" };

    readonly LiveProgramGraphVisualizer _graphViz = new();
    readonly LivePianoRollVisualizer _pianoViz = new();
    readonly LiveCodeInterpretationVisualizer _interpViz = new();
    LiveVisualizerWindow? _graphWindow;
    LiveVisualizerWindow? _pianoWindow;
    LiveVisualizerWindow? _interpWindow;
    LiveVisualizerModel _vizModel = new(null, 0, 1, 1, 120, null, null);

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

        _compileButton.Click += (_, _) => CompileRequested?.Invoke(this, EventArgs.Empty);
        _demoButton.Click += (_, _) => DemoRequested?.Invoke(this, EventArgs.Empty);
        _loadDefaultButton.Click += (_, _) =>
        {
            _editor.Text = LiveDemoCatalog.DefaultBuffer;
            _editor.FocusEditor();
        };
        _editor.CompileRequested += (_, _) => CompileRequested?.Invoke(this, EventArgs.Empty);
        _dashboard.PresetSelected += preset => PresetSelected?.Invoke(preset);

        _graphWindowButton.Click += (_, _) => OpenVisualizer(ref _graphWindow, _graphViz);
        _pianoWindowButton.Click += (_, _) => OpenVisualizer(ref _pianoWindow, _pianoViz);
        _interpWindowButton.Click += (_, _) => OpenVisualizer(ref _interpWindow, _interpViz);

        KeyDown += OnWorkspaceKeyDown;
        Focusable = true;
    }

    public event EventHandler? CompileRequested;
    public event EventHandler? DemoRequested;
    public event Action<LiveProgramPreset>? PresetSelected;

    public string SourceText => _editor.Text;

    public SwapPolicy SelectedSwapPolicy =>
        _swapPolicy.SelectedItem is SwapPolicy policy ? policy : SwapPolicy.Immediately;

    public void SetEditorDocument(string source)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _editor.Text = source;
            _editor.FocusEditor();
        });
    }

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

        _vizModel = new LiveVisualizerModel(
            Graph: state.Graph,
            Beat: state.Snapshot?.Beat ?? 0m,
            Bar: state.Snapshot?.Bar ?? 1,
            Phrase: state.Snapshot?.Phrase ?? 1,
            Bpm: state.Snapshot?.Bpm ?? 120m,
            ActivePreset: state.CurrentPresetName,
            SourceExcerpt: Truncate(state.EditorSource ?? SourceText, 600));

        _graphViz.Bind(_vizModel);
        _pianoViz.Bind(_vizModel);
        _interpViz.Bind(_vizModel);
        _graphWindow?.Bind(_vizModel);
        _pianoWindow?.Bind(_vizModel);
        _interpWindow?.Bind(_vizModel);
    }

    void OpenVisualizer(ref LiveVisualizerWindow? window, ILiveVisualizer visualizer)
    {
        if (window is { IsVisible: true })
        {
            window.Activate();
            window.Bind(_vizModel);
            return;
        }

        var opened = new LiveVisualizerWindow(visualizer);
        opened.Bind(_vizModel);
        opened.Closed += (_, _) =>
        {
            if (ReferenceEquals(opened, _graphWindow)) _graphWindow = null;
            else if (ReferenceEquals(opened, _pianoWindow)) _pianoWindow = null;
            else if (ReferenceEquals(opened, _interpWindow)) _interpWindow = null;
        };
        window = opened;
        opened.Show();
    }

    static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    void OnWorkspaceKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5)
        {
            CompileRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    Control BuildToolbar()
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

    Control BuildTitleCluster()
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
            Text = "Demos load into the editor. F5 compiles Live DSL. Ctrl+Space for completion. Open visualizer windows for interpretation.",
            Foreground = MutedBrush,
            FontSize = 12,
        });
        panel.Children.Add(_launcherStatus);
        return panel;
    }

    Control BuildActionCluster()
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
        _swapPolicy.MinWidth = 140;

        StyleActionButton(_compileButton, primary: true);
        StyleActionButton(_demoButton, primary: false);
        StyleActionButton(_loadDefaultButton, primary: false);
        StyleActionButton(_graphWindowButton, primary: false);
        StyleActionButton(_pianoWindowButton, primary: false);
        StyleActionButton(_interpWindowButton, primary: false);

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
        panel.Children.Add(_graphWindowButton);
        panel.Children.Add(_pianoWindowButton);
        panel.Children.Add(_interpWindowButton);
        return panel;
    }

    static void StyleActionButton(Button button, bool primary)
    {
        button.Padding = new Thickness(12, 8);
        button.CornerRadius = new CornerRadius(8);
        button.Background = new SolidColorBrush(Color.Parse(primary ? "#1D4ED8" : "#334155"));
        button.Foreground = Brushes.White;
    }
}
