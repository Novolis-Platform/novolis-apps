using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ManuscriptStudio.Components;
using ManuscriptStudio.Core;
using ManuscriptStudio.Extensions.BookAuthoring;
using ManuscriptStudio.Extensions.GenericMarkdown;
using Novolis.Avalonia.Markdown;
using Novolis.Avalonia.Studio;

namespace ManuscriptStudio;

internal sealed class MainWindow : Window
{
    private readonly EditorSession _session;
    private readonly ManuscriptSettingsStore _settings;
    private readonly ManuscriptExtensionRegistry _registry;
    private readonly MarkdownAuthoringWorkspace _authoring;
    private readonly ComboBox _modeCombo = new() { MinWidth = 160, Margin = new Thickness(0, 0, 8, 0) };

    private StudioFeedback _feedback = null!;
    private ManuscriptHostContext _hostContext = null!;
    private IManuscriptExtension _activeExtension = null!;
    private DispatcherTimer? _previewTimer;

    public MainWindow(EditorSession session, ManuscriptSettingsStore settings, ManuscriptExtensionRegistry registry)
    {
        _session = session;
        _settings = settings;
        _registry = registry;
        _authoring = new MarkdownAuthoringWorkspace(settings);

        Title = "Manuscript Studio";
        Width = 1400;
        Height = 900;

        Content = BuildLayout();
        KeyDown += OnKeyDown;
        Opened += OnOpened;

        _authoring.EditorTextChanged += OnEditorTextChanged;
        _authoring.SaveRequested += (_, _) => SaveCurrent();
        _authoring.PreviewRefreshRequested += (_, _) => RefreshPreview();
        _authoring.SettingsChanged += (_, _) => UpdateStatus();
        _modeCombo.SelectionChanged += OnModeChanged;
    }

    private Control BuildLayout()
    {
        var chrome = StudioChrome.Create();
        _feedback = chrome.CreateFeedback();

        _hostContext = new ManuscriptHostContext
        {
            Authoring = _authoring,
            Session = _session,
            Settings = _settings,
            Feedback = _feedback,
            RequestPreviewRefresh = RefreshPreview,
            GetEditorText = () => _authoring.Editor.Text ?? string.Empty,
            SetEditorText = text =>
            {
                _authoring.SetEditorText(text);
                _session.EditorText = text;
            },
            PickFolderAsync = PickFolderAsync,
            PickExportFolderAsync = PickExportFolderAsync,
            MainWindow = this,
            UpdateStatus = UpdateStatus,
            UpdateDirtyIndicator = UpdateDirtyIndicator,
            RefreshRightRail = RefreshPreview,
            GetRightRailViewId = GetRightRailViewId,
            SetRightRailViewId = SetRightRailViewId,
            SetEditorZoomScale = scale => _authoring.Editor.ZoomScale = scale,
            OnPreviewZoomScaleChanged = scale => _authoring.OnPreviewZoomChanged(scale),
        };

        foreach (var ext in _registry.All)
            _modeCombo.Items.Add(ext.DisplayName);

        var appBar = StudioWorkspace.CreateToolbarRow();
        appBar.Children.Add(_modeCombo);

        var statusBar = new DockPanel();
        DockPanel.SetDock(chrome.FlashLine, Dock.Bottom);
        DockPanel.SetDock(chrome.StatusLine, Dock.Bottom);
        statusBar.Children.Add(chrome.FlashLine);
        statusBar.Children.Add(chrome.StatusLine);

        var shell = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        var workspaceHost = new Grid();
        workspaceHost.Children.Add(_authoring);
        workspaceHost.Children.Add(chrome.BusyOverlay);

        Grid.SetRow(appBar, 0);
        shell.Children.Add(appBar);
        Grid.SetRow(workspaceHost, 1);
        shell.Children.Add(workspaceHost);
        Grid.SetRow(statusBar, 2);
        shell.Children.Add(statusBar);

        return shell;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        _settings.Load();
        _authoring.ApplySettings();

        if (string.IsNullOrWhiteSpace(_settings.Settings.ContentRoot) && Directory.Exists("D:\\repos\\books"))
            _settings.Settings.ContentRoot = "D:\\repos\\books";

        _previewTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(300), DispatcherPriority.Background, (_, _) => RefreshPreview());
        _previewTimer.Start();

        var activeId = _settings.Settings.ActiveExtensionId;
        _activeExtension = _registry.GetById(activeId);
        var modeIndex = _registry.All.ToList().FindIndex(ext => ext.Id == _activeExtension.Id);
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
        _authoring.FocusEditor();
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

        _authoring.Editor.HighlightingProfile = extension.Id == BookAuthoringExtension.ExtensionId
            ? MarkdownSourceHighlightingProfile.BookAuthoring
            : MarkdownSourceHighlightingProfile.Markdown;

        TrimExtensionActions(_authoring.NavigationActionBar, 0);
        TrimExtensionActions(_authoring.EditorActionBar, _authoring.EditorBarBuiltInChildCount);
        TrimExtensionActions(_authoring.PreviewActionBar, _authoring.PreviewBarBuiltInChildCount);

        extension.ConfigureNavigationBar(_authoring.NavigationActionBar, _hostContext);
        extension.ConfigureEditorBar(_authoring.EditorActionBar, _hostContext);
        extension.ConfigurePreviewBar(_authoring.PreviewActionBar, _hostContext);

        _authoring.SetNavigationContent(extension.CreateLeftRail(_hostContext));
        RebuildPreview();
        extension.OnActivated(_hostContext);
    }

    private static void TrimExtensionActions(StackPanel bar, int keepCount)
    {
        while (bar.Children.Count > keepCount)
            bar.Children.RemoveAt(bar.Children.Count - 1);
    }

    private void RebuildPreview()
    {
        var viewId = GetRightRailViewId();
        _authoring.SetPreviewContent(_activeExtension.CreateRightRail(_hostContext, viewId));
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
        _session.EditorText = _authoring.Editor.Text ?? string.Empty;
        UpdateDirtyIndicator();
        UpdateStatus();
    }

    private void RefreshPreview() =>
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
        _authoring.UpdateDirtyIndicator(_session.IsDirty);

    private void UpdateStatus()
    {
        var path = _session.SelectedFilePath ?? "No file selected";
        var words = _session.CountWords(_session.EditorText);
        var dirty = _session.IsDirty ? " (unsaved)" : string.Empty;
        var editor = _settings.Settings.Editor;
        _feedback.SetStatus(
            $"{path}{dirty} — {words} words — ed {Math.Round(_authoring.Editor.ZoomScale * 100)}% · pv {Math.Round(editor.PreviewZoomScale * 100)}%");
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
}
