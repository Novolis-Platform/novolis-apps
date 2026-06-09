using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaEdit;
using ManuscriptStudio.Core;
using Novolis.Avalonia.Markdown;
using Novolis.Avalonia.Studio;

namespace ManuscriptStudio.Components;

/// <summary>
/// Three-pane markdown authoring surface: navigation, editor, and preview,
/// each with its own action bar and resizable column splitters.
/// </summary>
internal sealed class MarkdownAuthoringWorkspace : Grid
{
    private const double MinSideWidth = 120;

    private readonly ColumnDefinition _leftCol = new(280, GridUnitType.Pixel);
    private readonly ColumnDefinition _rightCol = new(420, GridUnitType.Pixel);
    private readonly ManuscriptSettingsStore _settings;

    private readonly AuthoringPane _navigationPane = new();
    private readonly AuthoringPane _editorPane = new();
    private readonly AuthoringPane _previewPane = new();
    private readonly Grid _navigationBody = new();
    private readonly Grid _previewBody = new();

    private readonly ToggleButton _wrapToggle = new();
    private readonly ToggleButton _lightPreviewToggle = new();
    private readonly ToggleButton _syncZoomToggle = new();
    private readonly TextBlock _editorZoomLabel = new();
    private readonly TextBlock _previewZoomLabel = new();
    private readonly TextBlock _dirtyIndicator = new()
    {
        Margin = new Thickness(8, 0),
        VerticalAlignment = VerticalAlignment.Center,
        Foreground = Brushes.Orange,
    };

    private readonly Button _saveButton = ViewPaneControls.CreateActionButton("Save");
    private readonly Button _editorZoomOut = new();
    private readonly Button _editorZoomIn = new();
    private readonly Button _editorZoomReset = new();
    private readonly Button _previewZoomOut = new();
    private readonly Button _previewZoomIn = new();
    private readonly Button _previewZoomReset = new();

    private bool _suppressEditorChange;

    public MarkdownAuthoringWorkspace(ManuscriptSettingsStore settings)
    {
        _settings = settings;
        RestoreColumnWidths();

        Editor = new MarkdownSourceEditor
        {
            PlaceholderText = "Select a file or chapter…",
            Margin = new Thickness(4, 0, 4, 4),
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        BuildEditorActionBar();
        BuildPreviewActionBar();
        EditorBarBuiltInChildCount = _editorPane.ActionBar.Children.Count;
        PreviewBarBuiltInChildCount = _previewPane.ActionBar.Children.Count;
        _editorPane.SetBody(Editor);
        _navigationPane.SetBody(_navigationBody);
        _previewPane.SetBody(_previewBody);

        ColumnDefinitions = new ColumnDefinitions
        {
            _leftCol,
            new ColumnDefinition(GridLength.Auto),
            new ColumnDefinition(1, GridUnitType.Star),
            new ColumnDefinition(GridLength.Auto),
            _rightCol,
        };

        var splitterLeft = CreateSplitter();
        splitterLeft.DragCompleted += (_, _) => PersistLayout();
        var splitterRight = CreateSplitter();
        splitterRight.DragCompleted += (_, _) => PersistLayout();

        var leftHost = BorderPane(_navigationPane, new Thickness(0, 0, 1, 0));
        var rightHost = BorderPane(_previewPane, new Thickness(1, 0, 0, 0));

        Grid.SetColumn(leftHost, 0);
        Children.Add(leftHost);
        Grid.SetColumn(splitterLeft, 1);
        Children.Add(splitterLeft);
        Grid.SetColumn(_editorPane, 2);
        Children.Add(_editorPane);
        Grid.SetColumn(splitterRight, 3);
        Children.Add(splitterRight);
        Grid.SetColumn(rightHost, 4);
        Children.Add(rightHost);

        Editor.TextChanged += OnEditorTextChanged;
        Editor.PropertyChanged += OnEditorPropertyChanged;
        _saveButton.Click += (_, _) => SaveRequested?.Invoke(this, EventArgs.Empty);

        Editor.Loaded += (_, _) => AttachEditorWheelZoom();
        Loaded += (_, _) => AttachEditorWheelZoom();
    }

    private bool _editorWheelZoomAttached;

    private void AttachEditorWheelZoom()
    {
        if (_editorWheelZoomAttached)
            return;

        var textEditor = Editor.GetVisualDescendants().OfType<TextEditor>().FirstOrDefault();
        if (textEditor is null)
            return;

        _editorWheelZoomAttached = true;
        PointerWheelZoomRouting.Attach(textEditor, () => Editor.ZoomScale, scale => Editor.ZoomScale = scale);
    }

    public MarkdownSourceEditor Editor { get; }

    public StackPanel NavigationActionBar => _navigationPane.ActionBar;

    public StackPanel EditorActionBar => _editorPane.ActionBar;

    public StackPanel PreviewActionBar => _previewPane.ActionBar;

    /// <summary>Children count on the editor bar before extension-specific controls are appended.</summary>
    public int EditorBarBuiltInChildCount { get; private set; }

    /// <summary>Children count on the preview bar before extension-specific controls are appended.</summary>
    public int PreviewBarBuiltInChildCount { get; private set; }

    public event EventHandler<TextChangedEventArgs>? EditorTextChanged;

    public event EventHandler? SaveRequested;

    public event EventHandler? PreviewRefreshRequested;

    public event EventHandler? SettingsChanged;

    public void SetNavigationContent(Control content)
    {
        _navigationBody.Children.Clear();
        _navigationBody.Children.Add(content);
    }

    public void SetPreviewContent(Control content)
    {
        _previewBody.Children.Clear();
        _previewBody.Children.Add(content);
        ApplyPreviewZoomToPane(ClampZoom(_settings.Settings.Editor.PreviewZoomScale));
    }

    public void ApplySettings()
    {
        var editor = _settings.Settings.Editor;
        Editor.WordWrap = editor.WordWrap;
        Editor.ZoomScale = ClampZoom(editor.EditorZoomScale);
        editor.PreviewZoomScale = ClampZoom(editor.PreviewZoomScale);
        _wrapToggle.IsChecked = editor.WordWrap;
        _lightPreviewToggle.IsChecked = editor.PreviewTheme.Equals("light", StringComparison.OrdinalIgnoreCase);
        _syncZoomToggle.IsChecked = editor.SyncZoom;
        UpdateEditorZoomLabel();
        UpdatePreviewZoomLabel();
        ApplyPreviewZoomToPane(editor.PreviewZoomScale);
    }

    public void PersistEditorSettings()
    {
        var editor = _settings.Settings.Editor;
        editor.WordWrap = Editor.WordWrap;
        editor.EditorZoomScale = Editor.ZoomScale;
        editor.SyncZoom = _syncZoomToggle.IsChecked == true;
        editor.PreviewTheme = _lightPreviewToggle.IsChecked == true ? "light" : "dark";
        _settings.Save();
        UpdateEditorZoomLabel();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetEditorText(string text)
    {
        _suppressEditorChange = true;
        Editor.Text = text;
        _suppressEditorChange = false;
    }

    public void UpdateDirtyIndicator(bool isDirty) =>
        _dirtyIndicator.Text = isDirty ? "● unsaved" : string.Empty;

    public void OnPreviewZoomChanged(double scale)
    {
        var clamped = ClampZoom(scale);
        _settings.Settings.Editor.PreviewZoomScale = clamped;
        UpdatePreviewZoomLabel();
        ApplyPreviewZoomToPane(clamped);

        if (_syncZoomToggle.IsChecked == true)
            Editor.ZoomScale = clamped;

        _settings.Save();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyPreviewZoomToPane(double zoom)
    {
        foreach (var pane in _previewBody.GetVisualDescendants().OfType<HtmlPreviewPane>())
            pane.ZoomScale = zoom;
    }

    public void FocusEditor() => Editor.FocusEditor();

    private void SetPreviewZoom(double scale) => OnPreviewZoomChanged(scale);

    private void BuildEditorActionBar()
    {
        var bar = _editorPane.ActionBar;

        bar.Children.Add(_saveButton);
        bar.Children.Add(StudioWorkspace.ToolbarSeparator());
        ViewPaneControls.AddWordWrapToggle(bar, _wrapToggle, OnWrapToggleClicked);
        bar.Children.Add(StudioWorkspace.ToolbarSeparator());
        ViewPaneControls.AddZoomCluster(
            bar,
            "Ed",
            _editorZoomOut,
            _editorZoomIn,
            _editorZoomReset,
            _editorZoomLabel,
            (_, _) => SetEditorZoom(Editor.ZoomScale - MarkdownZoom.Step),
            (_, _) => SetEditorZoom(Editor.ZoomScale + MarkdownZoom.Step),
            (_, _) => SetEditorZoom(1.0));
        bar.Children.Add(StudioWorkspace.ToolbarSeparator());
        bar.Children.Add(_dirtyIndicator);
    }

    private void BuildPreviewActionBar()
    {
        var bar = _previewPane.ActionBar;

        _lightPreviewToggle.Content = "Light";
        _lightPreviewToggle.Padding = new Thickness(8, 4);
        _lightPreviewToggle.Margin = new Thickness(0, 0, 4, 0);
        _lightPreviewToggle.Click += OnLightPreviewToggleClicked;
        bar.Children.Add(_lightPreviewToggle);

        _syncZoomToggle.Content = "Sync zoom";
        _syncZoomToggle.Padding = new Thickness(8, 4);
        _syncZoomToggle.Margin = new Thickness(0, 0, 4, 0);
        _syncZoomToggle.Click += OnSyncZoomToggleClicked;
        bar.Children.Add(_syncZoomToggle);

        bar.Children.Add(StudioWorkspace.ToolbarSeparator());
        ViewPaneControls.AddZoomCluster(
            bar,
            "Pv",
            _previewZoomOut,
            _previewZoomIn,
            _previewZoomReset,
            _previewZoomLabel,
            (_, _) => SetPreviewZoom(_settings.Settings.Editor.PreviewZoomScale - MarkdownZoom.Step),
            (_, _) => SetPreviewZoom(_settings.Settings.Editor.PreviewZoomScale + MarkdownZoom.Step),
            (_, _) => SetPreviewZoom(1.0));
    }

    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressEditorChange)
            return;

        EditorTextChanged?.Invoke(this, e);
    }

    private void OnEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != MarkdownSourceEditor.ZoomScaleProperty)
            return;

        _settings.Settings.Editor.EditorZoomScale = Editor.ZoomScale;
        UpdateEditorZoomLabel();

        if (_syncZoomToggle.IsChecked == true)
            SetPreviewZoom(Editor.ZoomScale);

        _settings.Save();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnWrapToggleClicked(object? sender, RoutedEventArgs e)
    {
        Editor.WordWrap = _wrapToggle.IsChecked == true;
        PersistEditorSettings();
    }

    private void OnLightPreviewToggleClicked(object? sender, RoutedEventArgs e)
    {
        PersistEditorSettings();
        PreviewRefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSyncZoomToggleClicked(object? sender, RoutedEventArgs e)
    {
        PersistEditorSettings();
        if (_syncZoomToggle.IsChecked == true)
            SetPreviewZoom(Editor.ZoomScale);
    }

    private void SetEditorZoom(double scale)
    {
        Editor.ZoomScale = ClampZoom(scale);
        _settings.Settings.Editor.EditorZoomScale = Editor.ZoomScale;
        UpdateEditorZoomLabel();

        if (_syncZoomToggle.IsChecked == true)
            SetPreviewZoom(Editor.ZoomScale);
        else
            _settings.Save();

        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateEditorZoomLabel() =>
        ViewPaneControls.UpdateZoomLabel(_editorZoomLabel, Editor.ZoomScale);

    private void UpdatePreviewZoomLabel() =>
        ViewPaneControls.UpdateZoomLabel(_previewZoomLabel, _settings.Settings.Editor.PreviewZoomScale);

    private void RestoreColumnWidths()
    {
        var layout = _settings.Settings.Layout;
        _leftCol.Width = new GridLength(Math.Max(MinSideWidth, layout.LeftColumnPixels), GridUnitType.Pixel);
        _rightCol.Width = new GridLength(Math.Max(MinSideWidth, layout.RightColumnPixels), GridUnitType.Pixel);
    }

    private void PersistLayout()
    {
        if (_leftCol.Width.IsAbsolute)
            _settings.Settings.Layout.LeftColumnPixels = _leftCol.Width.Value;
        if (_rightCol.Width.IsAbsolute)
            _settings.Settings.Layout.RightColumnPixels = _rightCol.Width.Value;
        _settings.Save();
    }

    private static double ClampZoom(double scale) =>
        Math.Clamp(scale, MarkdownZoom.Minimum, MarkdownZoom.Maximum);

    private static Border BorderPane(Control child, Thickness borderThickness) =>
        new()
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = borderThickness,
            Child = child,
        };

    private static GridSplitter CreateSplitter() =>
        new()
        {
            Width = 5,
            ResizeDirection = GridResizeDirection.Columns,
            Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
        };
}
