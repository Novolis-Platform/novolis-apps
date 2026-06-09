using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ManuscriptStudio.Core;
using ManuscriptStudio.Extensions.GenericMarkdown;
using Novolis.Avalonia.Studio;
using TheArtOfDev.HtmlRenderer.Avalonia;

namespace ManuscriptStudio;

internal sealed class MainWindow : Window
{
    private readonly EditorSession _session;
    private readonly ManuscriptSettingsStore _settings;
    private readonly ManuscriptExtensionRegistry _registry;

    private readonly TextBox _editor = new()
    {
        AcceptsReturn = true,
        AcceptsTab = true,
        TextWrapping = TextWrapping.Wrap,
        FontFamily = new FontFamily("Consolas,Courier New,monospace"),
        PlaceholderText = "Select a file or chapter…",
    };
    private readonly HtmlPanel _preview = new() { Margin = new Thickness(8) };
    private readonly TextBlock _dirtyIndicator = new()
    {
        Margin = new Thickness(8, 0),
        VerticalAlignment = VerticalAlignment.Center,
        Foreground = Brushes.Orange,
    };
    private readonly ComboBox _modeCombo = new() { MinWidth = 160, Margin = new Thickness(0, 0, 8, 0) };

    private readonly StackPanel _extensionToolbar = new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 0,
        Margin = new Thickness(0, 0, 4, 0),
    };

    private Grid _leftRailHost = new();
    private StudioFeedback _feedback = null!;
    private ManuscriptHostContext _hostContext = null!;
    private IManuscriptExtension _activeExtension = null!;
    private DispatcherTimer? _previewTimer;
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
        _modeCombo.SelectionChanged += OnModeChanged;
    }

    private Control BuildLayout()
    {
        var chrome = StudioChrome.Create();
        _feedback = chrome.CreateFeedback();

        _hostContext = new ManuscriptHostContext
        {
            Session = _session,
            Settings = _settings,
            Feedback = _feedback,
            RequestPreviewRefresh = RefreshPreview,
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
        };

        foreach (var ext in _registry.All)
            _modeCombo.Items.Add(ext.DisplayName);

        var saveButton = ToolbarButton("Save");
        saveButton.Click += (_, _) => SaveCurrent();

        var toolbar = StudioWorkspace.CreateToolbarRow();
        toolbar.Children.Add(_modeCombo);
        toolbar.Children.Add(StudioWorkspace.ToolbarSeparator());
        toolbar.Children.Add(saveButton);
        toolbar.Children.Add(StudioWorkspace.ToolbarSeparator());
        toolbar.Children.Add(_extensionToolbar);
        toolbar.Children.Add(StudioWorkspace.ToolbarSeparator());
        toolbar.Children.Add(_dirtyIndicator);

        var editorScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = _editor,
        };

        var center = StudioWorkspace.CreateCenterColumn(toolbar, editorScroll);

        var rightRail = new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1, 0, 0, 0),
            Child = new ScrollViewer
            {
                Content = _preview,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            },
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

        if (string.IsNullOrWhiteSpace(_settings.Settings.ContentRoot) && Directory.Exists("D:\\repos\\books"))
            _settings.Settings.ContentRoot = "D:\\repos\\books";

        _previewTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(300), DispatcherPriority.Background, (_, _) => RefreshPreview());
        _previewTimer.Start();

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

        RefreshPreview();
        UpdateDirtyIndicator();
        UpdateStatus();
    }

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
        RefreshPreview();
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
    }

    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressEditorChange)
            return;

        _session.EditorText = _editor.Text ?? string.Empty;
        UpdateDirtyIndicator();
        UpdateStatus();
    }

    private void RefreshPreview()
    {
        _preview.Text = _activeExtension.RenderPreviewHtml(_hostContext);
    }

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
        _feedback.SetStatus($"{path}{dirty} — {words} words");
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
