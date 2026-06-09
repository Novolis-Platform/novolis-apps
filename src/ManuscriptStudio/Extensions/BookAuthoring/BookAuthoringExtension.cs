using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using ManuscriptStudio.Core;
using ManuscriptStudio.Extensions.BookAuthoring.Content;
using ManuscriptStudio.Extensions.BookAuthoring.Helpers;
using ManuscriptStudio.Extensions.BookAuthoring.Navigation;
using ManuscriptStudio.Extensions.BookAuthoring.Rendering;
using ManuscriptStudio.Extensions.BookAuthoring.Views;
using Novolis.Avalonia.Markdown;
using Novolis.Avalonia.Studio;

namespace ManuscriptStudio.Extensions.BookAuthoring;

internal sealed class BookAuthoringExtension : IManuscriptExtension
{
    public const string ExtensionId = "book-authoring";

    private readonly ContentCatalog _catalog = new();
    private readonly BookPreviewRenderer _previewRenderer = new();
    private readonly MermaidViewExporter _mermaidExporter = new();

    private ManuscriptHostContext? _host;
    private ComboBox? _seriesCombo;
    private ComboBox? _bookCombo;
    private ComboBox? _navScopeCombo;
    private TreeView? _navTree;
    private TextBlock? _metadataSummary;
    private ComboBox? _viewCombo;
    private MarkdownPreviewPane? _previewPanel;
    private TextBox? _mermaidSource;
    private Grid? _rightRailRoot;
    private StackPanel? _mermaidHeader;
    private DockPanel? _mermaidPanel;
    private IReadOnlyList<SeriesInfo> _series = [];
    private BookInfo? _currentBook;
    private string _activeViewId = BookViewIds.Preview;

    public string Id => ExtensionId;
    public string DisplayName => "Book Authoring";
    public string DefaultRightRailViewId => BookViewIds.Preview;

    public Control CreateLeftRail(ManuscriptHostContext host)
    {
        _host = host;

        _seriesCombo = new ComboBox { PlaceholderText = "Series", Margin = new Avalonia.Thickness(4, 4, 4, 0) };
        _bookCombo = new ComboBox { PlaceholderText = "Book", Margin = new Avalonia.Thickness(4, 4, 4, 0) };
        _navTree = new TreeView { Margin = new Avalonia.Thickness(4) };
        _metadataSummary = new TextBlock
        {
            Margin = new Avalonia.Thickness(8, 4),
            FontSize = 11,
            Opacity = 0.85,
        };

        _seriesCombo.SelectionChanged += (_, _) => OnSeriesChanged();
        _bookCombo.SelectionChanged += (_, _) => OnBookChanged();
        _navTree.SelectionChanged += OnNavigationTreeSelectionChanged;

        var panel = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"),
        };
        Grid.SetRow(_seriesCombo, 0);
        Grid.SetRow(_bookCombo, 1);
        Grid.SetRow(_navTree, 2);
        Grid.SetRow(_metadataSummary, 3);
        panel.Children.Add(_seriesCombo);
        panel.Children.Add(_bookCombo);
        panel.Children.Add(_navTree);
        panel.Children.Add(_metadataSummary);

        ReloadCatalog();
        return new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
    }

    public void ConfigureNavigationBar(StackPanel bar, ManuscriptHostContext host)
    {
        _host = host;

        _navScopeCombo = new ComboBox { MinWidth = 120, Margin = new Avalonia.Thickness(0, 0, 4, 0) };
        _navScopeCombo.Items.Add("All");
        _navScopeCombo.Items.Add("Chapters");
        _navScopeCombo.Items.Add("References");
        var scope = BookNavigationScopeParser.Parse(host.Settings.Settings.BookAuthoring.NavigationScope);
        _navScopeCombo.SelectedIndex = scope switch
        {
            BookNavigationScope.Chapters => 1,
            BookNavigationScope.References => 2,
            _ => 0,
        };
        _navScopeCombo.SelectionChanged += (_, _) => OnNavigationScopeChanged();
        bar.Children.Add(_navScopeCombo);
        bar.Children.Add(ToolbarButton("Set content root…", OnSetContentRoot));
    }

    private void OnNavigationScopeChanged()
    {
        if (_host is null || _navScopeCombo is null || _navScopeCombo.SelectedIndex < 0)
            return;

        var scope = _navScopeCombo.SelectedIndex switch
        {
            1 => BookNavigationScope.Chapters,
            2 => BookNavigationScope.References,
            _ => BookNavigationScope.All,
        };
        _host.Settings.Settings.BookAuthoring.NavigationScope = BookNavigationScopeParser.ToSettingValue(scope);
        _host.Settings.Save();
        RebuildNavigationTree();
    }

    public void ConfigureEditorBar(StackPanel bar, ManuscriptHostContext host)
    {
        _host = host;

        bar.Children.Add(StudioWorkspace.ToolbarSeparator());

        var insertBtn = new Button { Content = "Insert", Margin = new Avalonia.Thickness(0, 0, 4, 0), Padding = new Avalonia.Thickness(8, 4) };
        insertBtn.Flyout = BuildInsertFlyout();
        bar.Children.Add(insertBtn);
        bar.Children.Add(ToolbarButton("Debug meta", ToggleDebugMetadata));

        var exportBtn = new Button { Content = "Export", Margin = new Avalonia.Thickness(0, 0, 4, 0), Padding = new Avalonia.Thickness(8, 4) };
        exportBtn.Flyout = BuildExportFlyout();
        bar.Children.Add(exportBtn);
    }

    public void ConfigurePreviewBar(StackPanel bar, ManuscriptHostContext host)
    {
        _host = host;

        bar.Children.Add(StudioWorkspace.ToolbarSeparator());

        _viewCombo = new ComboBox { MinWidth = 140, Margin = new Avalonia.Thickness(0, 0, 4, 0) };
        foreach (var view in GetRightRailViews())
            _viewCombo.Items.Add(view.DisplayName);

        var savedView = host.Settings.Settings.BookAuthoring.RightRailView;
        var viewIndex = GetRightRailViews().ToList().FindIndex(v => v.Id == savedView);
        _viewCombo.SelectedIndex = viewIndex >= 0 ? viewIndex : 0;
        _viewCombo.SelectionChanged += (_, _) => OnViewComboChanged();
        bar.Children.Add(_viewCombo);
    }

    public IReadOnlyList<RightRailViewDescriptor> GetRightRailViews() =>
        [
            RightRailViewDescriptor.Preview,
            new RightRailViewDescriptor(BookViewIds.Timeline, "Timeline"),
            new RightRailViewDescriptor(BookViewIds.Relationships, "Relationships"),
            new RightRailViewDescriptor(BookViewIds.Map, "Map"),
        ];

    public Control CreateRightRail(ManuscriptHostContext host, string viewId)
    {
        _host = host;
        _activeViewId = viewId;

        _previewPanel = CreatePreviewPane(host);
        _previewPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
        _previewPanel.VerticalAlignment = VerticalAlignment.Stretch;
        _mermaidSource = new TextBox
        {
            AcceptsReturn = true,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas,Courier New,monospace"),
            Margin = new Avalonia.Thickness(8),
        };

        var copyBtn = ToolbarButton("Copy", CopyMermaidSource);
        var saveBtn = ToolbarButton("Save .mmd", SaveMermaidSource);
        _mermaidHeader = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Avalonia.Thickness(8, 8, 8, 0),
            Children = { copyBtn, saveBtn },
        };

        _mermaidPanel = new DockPanel();
        DockPanel.SetDock(_mermaidHeader, Dock.Top);
        _mermaidPanel.Children.Add(_mermaidHeader);
        _mermaidPanel.Children.Add(_mermaidSource);

        _rightRailRoot = new Grid();
        _rightRailRoot.Children.Add(_previewPanel);
        _rightRailRoot.Children.Add(_mermaidPanel);

        ApplyViewVisibility(viewId);
        return _rightRailRoot;
    }

    public void OnRightRailViewChanged(ManuscriptHostContext host, string viewId)
    {
        _host = host;
        _activeViewId = viewId;
        ApplyViewVisibility(viewId);
        RefreshRightRailContent(viewId);
        SyncViewCombo(viewId);
    }

    public void OnActivated(ManuscriptHostContext host)
    {
        _host = host;
        ReloadCatalog();
    }

    public void OnDeactivated(ManuscriptHostContext host) => _host = null;

    private MarkdownPreviewPane CreatePreviewPane(ManuscriptHostContext host)
    {
        var pane = new MarkdownPreviewPane
        {
            ZoomScale = host.PreviewZoomScale,
            PreviewTheme = host.PreviewTheme,
        };
        pane.ZoomScaleChanged += (_, scale) => _host?.OnPreviewZoomScaleChanged(scale);
        return pane;
    }

    private void ApplyViewVisibility(string viewId)
    {
        if (_rightRailRoot is null || _previewPanel is null || _mermaidPanel is null)
            return;

        var isPreview = viewId == BookViewIds.Preview;
        _previewPanel.IsVisible = isPreview;
        _mermaidPanel.IsVisible = !isPreview;
    }

    private void RefreshRightRailContent(string viewId)
    {
        if (_host is null)
            return;

        if (viewId == BookViewIds.Preview)
        {
            if (_previewPanel is not null)
            {
                var debug = _host.Settings.Settings.BookAuthoring.DebugMetadata || (_currentBook?.DebugMode ?? false);
                _previewPanel.PreviewTheme = _host.PreviewTheme;
                _previewPanel.ZoomScale = _host.PreviewZoomScale;
                _previewPanel.DocumentBodyHtml = _previewRenderer.ToBodyHtml(_host.GetEditorText(), debug, _host.PreviewTheme);
            }

            return;
        }

        if (_mermaidSource is null || _currentBook is null)
        {
            if (_mermaidSource is not null)
                _mermaidSource.Text = _currentBook is null ? "Select a book to generate views." : string.Empty;
            return;
        }

        var (timeline, relationships, map) = _mermaidExporter.BuildAll(_currentBook);
        _mermaidSource.Text = viewId switch
        {
            BookViewIds.Timeline => timeline,
            BookViewIds.Relationships => relationships,
            BookViewIds.Map => map,
            _ => string.Empty,
        };
    }

    private void SyncViewCombo(string viewId)
    {
        if (_viewCombo is null)
            return;

        var idx = GetRightRailViews().ToList().FindIndex(v => v.Id == viewId);
        if (idx >= 0 && _viewCombo.SelectedIndex != idx)
            _viewCombo.SelectedIndex = idx;
    }

    private void OnViewComboChanged()
    {
        if (_host is null || _viewCombo is null || _viewCombo.SelectedIndex < 0)
            return;

        var views = GetRightRailViews();
        if (_viewCombo.SelectedIndex >= views.Count)
            return;

        var viewId = views[_viewCombo.SelectedIndex].Id;
        _host.SetRightRailViewId(viewId);
        OnRightRailViewChanged(_host, viewId);
    }

    private MenuFlyout BuildInsertFlyout()
    {
        var menu = new MenuFlyout();
        menu.Items.Add(MenuItem("[!date]", () => InsertMetadata("date", "2496.001")));
        menu.Items.Add(MenuItem("[!time]", () => InsertMetadata("time", "12:00")));
        menu.Items.Add(MenuItem("[!system]", () => InsertMetadata("system", "System name")));
        menu.Items.Add(MenuItem("[!location]", () => InsertMetadata("location", "Place")));
        menu.Items.Add(MenuItem("[!pov]", () => InsertMetadata("pov", "Character")));
        menu.Items.Add(MenuItem("[!characters]", () => InsertMetadata("characters", "Name, Name")));
        menu.Items.Add(MenuItem("Dialogue", InsertDialogue));
        menu.Items.Add(MenuItem("Thinking", InsertThinking));
        menu.Items.Add(MenuItem("Chapter tag", InsertChapterTag));
        return menu;
    }

    private MenuFlyout BuildExportFlyout()
    {
        var menu = new MenuFlyout();
        menu.Items.Add(MenuItem("Export PDF", ExportPdf));
        menu.Items.Add(MenuItem("Export view (.mmd)", ExportCurrentView));
        menu.Items.Add(MenuItem("Export all views", ExportAllViews));
        return menu;
    }

    private static MenuItem MenuItem(string label, Action onClick)
    {
        var item = new MenuItem { Header = label };
        item.Click += (_, _) => onClick();
        return item;
    }

    private async void CopyMermaidSource()
    {
        if (_host is null || _mermaidSource?.Text is null)
            return;

        var clipboard = TopLevel.GetTopLevel(_host.MainWindow)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(_mermaidSource.Text);
        _host.Feedback.Flash("Copied Mermaid source.");
    }

    private void SaveMermaidSource()
    {
        if (_host is null || _currentBook is null || _mermaidSource?.Text is null)
        {
            _host?.Feedback.FlashWarning("Nothing to save.");
            return;
        }

        try
        {
            _mermaidExporter.ExportView(_host.Settings.DataRoot, _currentBook, _activeViewId, _mermaidSource.Text);
            var path = Path.Combine(
                _mermaidExporter.ExportDirectory(_host.Settings.DataRoot, _currentBook),
                MermaidViewExporter.ViewFileName(_activeViewId));
            _host.Feedback.Flash($"Saved {path}");
        }
        catch (Exception ex)
        {
            _host.Feedback.FlashError($"Save failed: {ex.Message}");
        }
    }

    private void ExportCurrentView()
    {
        if (_host is null || _currentBook is null)
        {
            _host?.Feedback.FlashWarning("Select a book first.");
            return;
        }

        try
        {
            var (timeline, relationships, map) = _mermaidExporter.BuildAll(_currentBook);
            var mermaid = _activeViewId switch
            {
                BookViewIds.Timeline => timeline,
                BookViewIds.Relationships => relationships,
                BookViewIds.Map => map,
                _ => timeline,
            };
            var viewId = _activeViewId == BookViewIds.Preview ? BookViewIds.Timeline : _activeViewId;
            _mermaidExporter.ExportView(_host.Settings.DataRoot, _currentBook, viewId, mermaid);
            var path = Path.Combine(
                _mermaidExporter.ExportDirectory(_host.Settings.DataRoot, _currentBook),
                MermaidViewExporter.ViewFileName(viewId));
            _host.Feedback.Flash($"Exported {path}");
        }
        catch (Exception ex)
        {
            _host.Feedback.FlashError($"Export failed: {ex.Message}");
        }
    }

    private void ExportAllViews()
    {
        if (_host is null || _currentBook is null)
        {
            _host?.Feedback.FlashWarning("Select a book first.");
            return;
        }

        try
        {
            var (timeline, relationships, map) = _mermaidExporter.BuildAll(_currentBook);
            _mermaidExporter.ExportAll(_host.Settings.DataRoot, _currentBook, timeline, relationships, map);
            var dir = _mermaidExporter.ExportDirectory(_host.Settings.DataRoot, _currentBook);
            _host.Feedback.Flash($"Exported views to {dir}");
        }
        catch (Exception ex)
        {
            _host.Feedback.FlashError($"Export failed: {ex.Message}");
        }
    }

    private void ReloadCatalog()
    {
        if (_host is null || _seriesCombo is null || _bookCombo is null)
            return;

        var root = _host.Settings.Settings.ContentRoot;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            _series = [];
            _seriesCombo.ItemsSource = null;
            _bookCombo.ItemsSource = null;
            if (_navTree is not null)
                _navTree.Items.Clear();
            return;
        }

        try
        {
            _series = _catalog.Load(root);
            _seriesCombo.ItemsSource = _series.Select(s => s.Title).ToList();
            var savedSeries = _host.Settings.Settings.BookAuthoring.SeriesId;
            if (savedSeries is not null)
            {
                var idx = IndexOfSeries(_series, savedSeries);
                if (idx >= 0)
                    _seriesCombo.SelectedIndex = idx;
            }

            if (_seriesCombo.SelectedIndex < 0 && _series.Count > 0)
                _seriesCombo.SelectedIndex = 0;

            OnSeriesChanged();
        }
        catch (Exception ex)
        {
            _host.Feedback.FlashError($"Catalog load failed: {ex.Message}");
        }
    }

    private void OnSeriesChanged()
    {
        if (_host is null || _seriesCombo is null || _bookCombo is null)
            return;

        if (_seriesCombo.SelectedIndex < 0 || _seriesCombo.SelectedIndex >= _series.Count)
        {
            _bookCombo.ItemsSource = null;
            return;
        }

        var series = _series[_seriesCombo.SelectedIndex];
        _host.Settings.Settings.BookAuthoring.SeriesId = series.Id;
        _host.Settings.Save();

        _bookCombo.ItemsSource = series.Books.Select(b => b.Title).ToList();
        var savedBook = _host.Settings.Settings.BookAuthoring.BookId;
        if (savedBook is not null)
        {
            var idx = IndexOfBook(series.Books, savedBook);
            if (idx >= 0)
                _bookCombo.SelectedIndex = idx;
        }

        if (_bookCombo.SelectedIndex < 0 && series.Books.Count > 0)
            _bookCombo.SelectedIndex = 0;

        OnBookChanged();
    }

    private void OnBookChanged()
    {
        if (_host is null || _seriesCombo is null || _bookCombo is null || _navTree is null)
            return;

        if (_seriesCombo.SelectedIndex < 0 || _bookCombo.SelectedIndex < 0)
            return;

        var series = _series[_seriesCombo.SelectedIndex];
        if (_bookCombo.SelectedIndex >= series.Books.Count)
            return;

        _currentBook = series.Books[_bookCombo.SelectedIndex];
        _host.Settings.Settings.BookAuthoring.BookId = _currentBook.Id;
        _host.Settings.Save();

        RebuildNavigationTree();
        _host.RefreshRightRail();
    }

    private void RebuildNavigationTree()
    {
        if (_navTree is null)
            return;

        var series = GetCurrentSeries();
        var scope = BookNavigationScopeParser.Parse(_host?.Settings.Settings.BookAuthoring.NavigationScope);
        var nodes = BookNavigationTreeBuilder.Build(series, _currentBook, scope);
        _navTree.Items.Clear();
        foreach (var node in nodes)
            _navTree.Items.Add(CreateNavTreeItem(node));
    }

    private SeriesInfo? GetCurrentSeries()
    {
        if (_seriesCombo is null || _seriesCombo.SelectedIndex < 0 || _seriesCombo.SelectedIndex >= _series.Count)
            return null;

        return _series[_seriesCombo.SelectedIndex];
    }

    private static TreeViewItem CreateNavTreeItem(BookNavigationNode node)
    {
        var item = new TreeViewItem { Header = node.Label, Tag = node };
        if (node.IsFile)
            return item;

        item.IsExpanded = true;
        foreach (var child in node.Children)
            item.Items.Add(CreateNavTreeItem(child));

        return item;
    }

    private void OnNavigationTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_host is null || _navTree?.SelectedItem is not TreeViewItem { Tag: BookNavigationNode node } || !node.IsFile || node.FilePath is null)
            return;

        if (_host.Session.IsDirty)
            _host.Feedback.FlashWarning("Unsaved changes were discarded.");

        try
        {
            _host.Session.SelectFile(node.FilePath);
            _host.SetEditorText(_host.Session.EditorText);
            _host.RefreshRightRail();
            _host.UpdateDirtyIndicator();
            _host.UpdateStatus();
            UpdateMetadataSummary();
        }
        catch (Exception ex)
        {
            _host.Feedback.FlashError(ex.Message);
        }
    }

    private ChapterInfo? GetSelectedChapter()
    {
        if (_currentBook is null || _host?.Session.SelectedFilePath is null)
            return null;

        return _currentBook.Chapters.FirstOrDefault(c =>
            string.Equals(c.FilePath, _host.Session.SelectedFilePath, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateMetadataSummary()
    {
        if (_metadataSummary is null || _host is null)
            return;

        var rows = ChapterMetadata.ParseFromMarkdown(_host.GetEditorText());
        if (rows.Count == 0)
        {
            _metadataSummary.Text = string.Empty;
            return;
        }

        _metadataSummary.Text = string.Join(" · ", rows.Select(r => $"{r.Tag}: {r.Value}"));
    }

    private async void OnSetContentRoot()
    {
        if (_host is null)
            return;

        var path = await _host.PickFolderAsync();
        if (path is null)
            return;

        _host.Settings.Settings.ContentRoot = path;
        _host.Settings.Save();
        ReloadCatalog();
        _host.Feedback.Flash($"Content root: {path}");
    }

    private void InsertMetadata(string tag, string placeholder)
    {
        if (_host is null)
            return;

        var text = _host.GetEditorText();
        var updated = MetadataInsertHelper.InsertAtCursor(text, text.Length, tag, placeholder);
        _host.Session.EditorText = updated;
        _host.SetEditorText(updated);
        _host.RefreshRightRail();
        UpdateMetadataSummary();
    }

    private void InsertDialogue()
    {
        if (_host is null)
            return;

        var text = _host.GetEditorText();
        var updated = DialogueInsertHelper.InsertDialogueBlock(text, text.Length);
        _host.Session.EditorText = updated;
        _host.SetEditorText(updated);
        _host.RefreshRightRail();
    }

    private void InsertThinking()
    {
        if (_host is null)
            return;

        var text = _host.GetEditorText();
        var updated = DialogueInsertHelper.InsertThinkingBlock(text, text.Length);
        _host.Session.EditorText = updated;
        _host.SetEditorText(updated);
        _host.RefreshRightRail();
    }

    private void InsertChapterTag()
    {
        if (_host is null)
            return;

        var chapter = GetSelectedChapter();
        if (chapter is null)
        {
            _host.Feedback.FlashWarning("Select a chapter first.");
            return;
        }

        var text = _host.GetEditorText();
        var updated = MetadataInsertHelper.InsertChapterTag(text, 0, chapter.SortKey);
        _host.Session.EditorText = updated;
        _host.SetEditorText(updated);
        _host.RefreshRightRail();
    }

    private void ToggleDebugMetadata()
    {
        if (_host is null)
            return;

        _host.Settings.Settings.BookAuthoring.DebugMetadata = !_host.Settings.Settings.BookAuthoring.DebugMetadata;
        _host.Settings.Save();
        _host.RefreshRightRail();
        _host.Feedback.Flash(_host.Settings.Settings.BookAuthoring.DebugMetadata ? "Debug metadata on" : "Debug metadata off");
    }

    private async void ExportPdf()
    {
        if (_host is null || _currentBook is null)
        {
            _host?.Feedback.FlashWarning("Select a book first.");
            return;
        }

        var folder = await _host.PickExportFolderAsync();
        if (folder is null)
            folder = Path.Combine(_host.Settings.DataRoot, "exports", _currentBook.SeriesId ?? "standalone", _currentBook.Id);

        try
        {
            Directory.CreateDirectory(folder);
            var pdfPath = Path.Combine(folder, $"{_currentBook.Id}.pdf");
            BookPdfExporter.Export(_currentBook, pdfPath);
            _host.Feedback.Flash($"Exported {pdfPath}");
        }
        catch (Exception ex)
        {
            _host.Feedback.FlashError($"Export failed: {ex.Message}");
        }
    }

    private static int IndexOfSeries(IReadOnlyList<SeriesInfo> series, string seriesId)
    {
        for (var i = 0; i < series.Count; i++)
            if (series[i].Id.Equals(seriesId, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    private static int IndexOfBook(IReadOnlyList<BookInfo> books, string bookId)
    {
        for (var i = 0; i < books.Count; i++)
            if (books[i].Id.Equals(bookId, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    private static Button ToolbarButton(string label, Action onClick)
    {
        var btn = new Button { Content = label, Margin = new Avalonia.Thickness(0, 0, 4, 0), Padding = new Avalonia.Thickness(8, 4) };
        btn.Click += (_, _) => onClick();
        return btn;
    }
}
