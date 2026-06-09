using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ManuscriptStudio.Core;
using ManuscriptStudio.Extensions.GenericMarkdown;
using Novolis.Avalonia.Markdown;
using Novolis.Avalonia.Studio;

namespace ManuscriptStudio;

internal sealed class MainWindow : Window
{
    private readonly EditorSession _session;
    private readonly ManuscriptSettingsStore _settings;
    private readonly ManuscriptExtensionRegistry _registry;

    private readonly MarkdownSourceEditor _editor = new()
    {
        PlaceholderText = "Select a file or chapter…",
        Margin = new Thickness(8, 4, 8, 8),
        VerticalAlignment = VerticalAlignment.Stretch,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };
    private readonly TextBlock _dirtyIndicator = new()
    {
        Margin = new Thickness(8, 0),
        VerticalAlignment = VerticalAlignment.Center,
        Foreground = Brushes.Orange,
    };
    private readonly ComboBox _modeCombo = new() { MinWidth = 160, Margin = new Thickness(0, 0, 8, 0) };
    private readonly ToggleButton _wrapToggle = new() { Content = "Wrap", Padding = new Thickness(8, 4), Margin = new Thickness(0, 0, 4, 0) };
    private readonly ToggleButton _lightPreviewToggle = new() { Content = "Light preview", Padding = new Thickness(8, 4), Margin = new Thickness(0, 0, 4, 0) };
    private readonly ToggleButton _syncZoomToggle = new() { Content = "Sync zoom", Padding = new Thickness(8, 4), Margin = new Thickness(0, 0, 4, 0) };
    private readonly TextBlock _editorZoomLabel = new() { Margin = new Thickness(4, 0), VerticalAlignment = VerticalAlignment.Center, Opacity = 0.85 };

    private readonly StackPanel _extensionToolbar = new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 0,
        Margin = new Thickness(0, 0, 4, 0),
    };

    private readonly Grid _rightRailHost = new();
    private Grid _leftRailHost = new();
    private StudioFeedback _feedback = null!;
    private ManuscriptHostContext _hostContext = null!;
    private IManuscriptExtension _activeExtension = null!;
    private DispatcherTimer? _rightRailTimer;
    private bool _suppressEditorChange;

    public MainWindow(EditorSession session, ManuscriptSettingsStore settings, ManuscriptExtensionRegistry registry)
    {
        _session = session;
        _settings = settings;
        _registry = registry;

        Title = "Manuscript Studio";
        Width = 1400;
        Height = 900;

        Content = BuildLayout();
        KeyDown += OnKeyDown;
        Opened += OnOpened;
        _editor.TextChanged += OnEditorTextChanged;
        _editor.PropertyChanged += OnEditorPropertyChanged;
        _modeCombo.SelectionChanged += OnModeChanged;
        _wrapToggle.Click += OnWrapToggleClicked;
        _lightPreviewToggle.Click += OnLightPreviewToggleClicked;
        _syncZoomToggle.Click += OnSyncZoomToggleClicked;
    }

    private Control BuildLayout()
    {
        var chrome = StudioChrome.Create();
        _feedback = chrome.CreateFeedback();

        _hostContext = new ManuscriptHostContext
        {
            Editor = _editor,
            Session = _session,
            Settings = _settings,
            Feedback = _feedback,
            RequestPreviewRefresh = RefreshRightRail,
            GetEditorText = () => _editor.Text ?? string.Empty,
            SetEditorText = text =>
            {
                _suppressEditorChange = true;
                _editor.Text = text;
                _session.EditorText = text;
                _suppressEditorChange = false;
            },
            PickFolderAsync = PickFolderAsync,
            PickExportFolderAsync = PickExportFolderAsync,
            MainWindow = this,
            UpdateStatus = UpdateStatus,
            UpdateDirtyIndicator = UpdateDirtyIndicator,
            RefreshRightRail = RefreshRightRail,
            GetRightRailViewId = GetRightRailViewId,
            SetRightRailViewId = SetRightRailViewId,
            SetEditorZoomScale = scale => _editor.ZoomScale = scale,
        };

        foreach (var ext in _registry.All)
            _modeCombo.Items.Add(ext.DisplayName);

        var saveButton = ToolbarButton("Save");
        saveButton.Click += (_, _) => SaveCurrent();

        var zoomOut = ToolbarButton("−");
        zoomOut.Click += (_, _) => AdjustEditorZoom(-MarkdownZoom.Step);
        var zoomIn = ToolbarButton("+");
        zoomIn.Click += (_, _) => AdjustEditorZoom(MarkdownZoom.Step);
        var zoomReset = ToolbarButton("100%");
        zoomReset.Click += (_, _) => SetEditorZoom(1.0);

        var toolbar = StudioWorkspace.CreateToolbarRow();
        toolbar.Children.Add(_modeCombo);
        toolbar.Children.Add(StudioWorkspace.ToolbarSeparator());
        toolbar.Children.Add(saveButton);
        toolbar.Children.Add(StudioWorkspace.ToolbarSeparator());
        toolbar.Children.Add(_wrapToggle);
        toolbar.Children.Add(_lightPreviewToggle);
        toolbar.Children.Add(_syncZoomToggle);
        toolbar.Children.Add(zoomOut);
        toolbar.Children.Add(zoomIn);
        toolbar.Children.Add(zoomReset);
        toolbar.Children.Add(_editorZoomLabel);
        toolbar.Children.Add(StudioWorkspace.ToolbarSeparator());
        toolbar.Children.Add(_extensionToolbar);
        toolbar.Children.Add(StudioWorkspace.ToolbarSeparator());
        toolbar.Children.Add(_dirtyIndicator);

        var center = StudioWorkspace.CreateCenterColumn(toolbar, _editor);

        var rightRail = new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1, 0, 0, 0),
            Child = _rightRailHost,
        };

        var workspace = new ResizableStudioShell(_leftRailHost, center, rightRail, _settings);

        var statusBar = new DockPanel();
        DockPanel.SetDock(chrome.FlashLine, Dock.Bottom);
        DockPanel.SetDock(chrome.StatusLine, Dock.Bottom);
        statusBar.Children.Add(chrome.FlashLine);
        statusBar.Children.Add(chrome.StatusLine);

        var shell = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        var workspaceHost = new Grid();
        workspaceHost.Children.Add(workspace);
        workspaceHost.Children.Add(chrome.BusyOverlay);
        Grid.SetRow(workspaceHost, 0);
        shell.Children.Add(workspaceHost);
        Grid.SetRow(statusBar, 1);
        shell.Children.Add(statusBar);

        return shell;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        _settings.Load();
        ApplyEditorSettings();

        if (string.IsNullOrWhiteSpace(_settings.Settings.ContentRoot) && Directory.Exists("D:\\repos\\books"))
            _settings.Settings.ContentRoot = "D:\\repos\\books";

        _rightRailTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(300), DispatcherPriority.Background, (_, _) => RefreshRightRail());
        _rightRailTimer.Start();

        var activeId = _settings.Settings.ActiveExtensionId;
        _activeExtension = _registry.GetById(activeId);
        var modeIndex = _registry.All.ToList().FindIndex(e => e.Id == _activeExtension.Id);
        _modeCombo.SelectedIndex = modeIndex >= 0 ? modeIndex : 0;
        ActivateExtension(_activeExtension);

        var startup = Program.StartupFolder ?? _settings.Settings.LastWorkspaceRoot;
        if (startup is not null && _activeExtension.Id == GenericMarkdownExtension.ExtensionId)
        {
            try
            {
                _session.OpenWorkspace(startup);
                _activeExtension.OnActivated(_hostContext);
                _feedback.Flash($"Opened {startup}");
            }
            catch (Exception ex)
            {
                _feedback.FlashError($"Could not open workspace: {ex.Message}");
            }
        }

        RefreshRightRail();
        UpdateDirtyIndicator();
        UpdateStatus();
        _editor.FocusEditor();
    }

    private void ApplyEditorSettings()
    {
        var editor = _settings.Settings.Editor;
        _editor.WordWrap = editor.WordWrap;
        _editor.ZoomScale = ClampZoom(editor.EditorZoomScale);
        _wrapToggle.IsChecked = editor.WordWrap;
        _lightPreviewToggle.IsChecked = editor.PreviewTheme.Equals("light", StringComparison.OrdinalIgnoreCase);
        _syncZoomToggle.IsChecked = editor.SyncZoom;
        UpdateEditorZoomLabel();
    }

    private void SaveEditorSettings()
    {
        var editor = _settings.Settings.Editor;
        editor.WordWrap = _editor.WordWrap;
        editor.EditorZoomScale = _editor.ZoomScale;
        editor.SyncZoom = _syncZoomToggle.IsChecked == true;
        editor.PreviewTheme = _lightPreviewToggle.IsChecked == true ? "light" : "dark";
        _settings.Save();
        UpdateEditorZoomLabel();
    }

    private void OnWrapToggleClicked(object? sender, RoutedEventArgs e)
    {
        _editor.WordWrap = _wrapToggle.IsChecked == true;
        SaveEditorSettings();
    }

    private void OnLightPreviewToggleClicked(object? sender, RoutedEventArgs e)
    {
        SaveEditorSettings();
        RefreshRightRail();
    }

    private void OnSyncZoomToggleClicked(object? sender, RoutedEventArgs e)
    {
        SaveEditorSettings();
        if (_syncZoomToggle.IsChecked == true)
            SyncPreviewZoomToEditor();
    }

    private void OnEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != MarkdownSourceEditor.ZoomScaleProperty)
            return;

        _settings.Settings.Editor.EditorZoomScale = _editor.ZoomScale;
        UpdateEditorZoomLabel();

        if (_syncZoomToggle.IsChecked == true)
            SyncPreviewZoomToEditor();

        _settings.Save();
    }

    private void AdjustEditorZoom(double delta) =>
        SetEditorZoom(_editor.ZoomScale + delta);

    private void SetEditorZoom(double scale)
    {
        _editor.ZoomScale = ClampZoom(scale);
        _settings.Settings.Editor.EditorZoomScale = _editor.ZoomScale;
        UpdateEditorZoomLabel();

        if (_syncZoomToggle.IsChecked == true)
            SyncPreviewZoomToEditor();

        _settings.Save();
    }

    private void SyncPreviewZoomToEditor()
    {
        _settings.Settings.Editor.PreviewZoomScale = _editor.ZoomScale;
        _settings.Save();
        RefreshRightRail();
    }

    private void UpdateEditorZoomLabel() =>
        _editorZoomLabel.Text = $"{Math.Round(_editor.ZoomScale * 100)}%";

    private static double ClampZoom(double scale) =>
        Math.Clamp(scale, MarkdownZoom.Minimum, MarkdownZoom.Maximum);

    private void OnModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_modeCombo.SelectedIndex < 0 || _modeCombo.SelectedIndex >= _registry.All.Count)
            return;

        var next = _registry.All[_modeCombo.SelectedIndex];
        if (next.Id == _activeExtension.Id)
            return;

        _activeExtension.OnDeactivated(_hostContext);
        ActivateExtension(next);
        _settings.Settings.ActiveExtensionId = next.Id;
        _settings.Save();
        RefreshRightRail();
        _feedback.Flash($"Mode: {next.DisplayName}");
    }

    private void ActivateExtension(IManuscriptExtension extension)
    {
        _activeExtension = extension;
        _leftRailHost.Children.Clear();
        _leftRailHost.Children.Add(extension.CreateLeftRail(_hostContext));

        _extensionToolbar.Children.Clear();
        extension.ConfigureToolbar(_extensionToolbar, _hostContext);
        extension.OnActivated(_hostContext);
        RebuildRightRail();
    }

    private void RebuildRightRail()
    {
        _rightRailHost.Children.Clear();
        var viewId = GetRightRailViewId();
        _rightRailHost.Children.Add(_activeExtension.CreateRightRail(_hostContext, viewId));
        _activeExtension.OnRightRailViewChanged(_hostContext, viewId);
    }

    private string GetRightRailViewId()
    {
        if (_activeExtension.Id == Extensions.BookAuthoring.BookAuthoringExtension.ExtensionId)
            return _settings.Settings.BookAuthoring.RightRailView;

        return _activeExtension.DefaultRightRailViewId;
    }

    private void SetRightRailViewId(string viewId)
    {
        if (_activeExtension.Id == Extensions.BookAuthoring.BookAuthoringExtension.ExtensionId)
        {
            _settings.Settings.BookAuthoring.RightRailView = viewId;
            _settings.Save();
        }
    }

    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressEditorChange)
            return;

        _session.EditorText = _editor.Text ?? string.Empty;
        UpdateDirtyIndicator();
        UpdateStatus();
    }

    private void RefreshRightRail() =>
        _activeExtension.OnRightRailViewChanged(_hostContext, GetRightRailViewId());

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            SaveCurrent();
            e.Handled = true;
        }
    }

    private void SaveCurrent()
    {
        if (_session.SelectedFilePath is null)
        {
            _feedback.FlashWarning("No file selected.");
            return;
        }

        try
        {
            _session.SaveSelected();
            UpdateDirtyIndicator();
            UpdateStatus();
            _feedback.Flash("Saved.");
        }
        catch (Exception ex)
        {
            _feedback.FlashError($"Save failed: {ex.Message}");
        }
    }

    private void UpdateDirtyIndicator() =>
        _dirtyIndicator.Text = _session.IsDirty ? "● unsaved" : string.Empty;

    private void UpdateStatus()
    {
        var path = _session.SelectedFilePath ?? "No file selected";
        var words = _session.CountWords(_session.EditorText);
        var dirty = _session.IsDirty ? " (unsaved)" : string.Empty;
        _feedback.SetStatus($"{path}{dirty} — {words} words — editor {Math.Round(_editor.ZoomScale * 100)}%");
    }

    private async Task<string?> PickFolderAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
            return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select folder",
            AllowMultiple = false,
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    private async Task<string?> PickExportFolderAsync() => await PickFolderAsync();

    private static Button ToolbarButton(string label) =>
        new() { Content = label, Margin = new Thickness(0, 0, 4, 0), Padding = new Thickness(10, 4) };
}
