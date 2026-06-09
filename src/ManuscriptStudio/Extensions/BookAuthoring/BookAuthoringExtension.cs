using Avalonia.Controls;
using Avalonia.Layout;
using ManuscriptStudio.Core;
using ManuscriptStudio.Extensions.BookAuthoring.Content;
using ManuscriptStudio.Extensions.BookAuthoring.Helpers;
using ManuscriptStudio.Extensions.BookAuthoring.Rendering;
using Novolis.Avalonia.Studio;

namespace ManuscriptStudio.Extensions.BookAuthoring;

internal sealed class BookAuthoringExtension : IManuscriptExtension
{
    public const string ExtensionId = "book-authoring";

    private readonly ContentCatalog _catalog = new();
    private readonly BookPreviewRenderer _previewRenderer = new();

    private ManuscriptHostContext? _host;
    private ComboBox? _seriesCombo;
    private ComboBox? _bookCombo;
    private ListBox? _chapterList;
    private TextBlock? _metadataSummary;
    private IReadOnlyList<SeriesInfo> _series = [];
    private BookInfo? _currentBook;

    public string Id => ExtensionId;
    public string DisplayName => "Book Authoring";

    public Control CreateLeftRail(ManuscriptHostContext host)
    {
        _host = host;

        _seriesCombo = new ComboBox { PlaceholderText = "Series", Margin = new Avalonia.Thickness(4, 4, 4, 0) };
        _bookCombo = new ComboBox { PlaceholderText = "Book", Margin = new Avalonia.Thickness(4, 4, 4, 0) };
        _chapterList = new ListBox { Margin = new Avalonia.Thickness(4) };
        _metadataSummary = new TextBlock
        {
            Margin = new Avalonia.Thickness(8, 4),
            FontSize = 11,
            Opacity = 0.85,
        };

        _seriesCombo.SelectionChanged += (_, _) => OnSeriesChanged();
        _bookCombo.SelectionChanged += (_, _) => OnBookChanged();
        _chapterList.SelectionChanged += (_, _) => OnChapterSelected();

        var panel = new StackPanel();
        panel.Children.Add(ToolbarButton("Set content root…", OnSetContentRoot));
        panel.Children.Add(_seriesCombo);
        panel.Children.Add(_bookCombo);
        panel.Children.Add(new TextBlock { Text = "Chapters", Margin = new Avalonia.Thickness(8, 8, 8, 0), Opacity = 0.7 });
        panel.Children.Add(_chapterList);
        panel.Children.Add(_metadataSummary);

        ReloadCatalog();
        return new ScrollViewer { Content = panel };
    }

    public void ConfigureToolbar(StackPanel toolbar, ManuscriptHostContext host)
    {
        _host = host;
        toolbar.Children.Add(ToolbarButton("[!date]", () => InsertMetadata("date", "2496.001")));
        toolbar.Children.Add(ToolbarButton("[!time]", () => InsertMetadata("time", "12:00")));
        toolbar.Children.Add(ToolbarButton("[!system]", () => InsertMetadata("system", "System name")));
        toolbar.Children.Add(ToolbarButton("[!location]", () => InsertMetadata("location", "Place")));
        toolbar.Children.Add(ToolbarButton("[!pov]", () => InsertMetadata("pov", "Character")));
        toolbar.Children.Add(ToolbarButton("[!characters]", () => InsertMetadata("characters", "Name, Name")));
        toolbar.Children.Add(StudioWorkspace.ToolbarSeparator());
        toolbar.Children.Add(ToolbarButton("Dialogue", () => InsertDialogue()));
        toolbar.Children.Add(ToolbarButton("Thinking", () => InsertThinking()));
        toolbar.Children.Add(ToolbarButton("Chapter tag", InsertChapterTag));
        toolbar.Children.Add(StudioWorkspace.ToolbarSeparator());
        toolbar.Children.Add(ToolbarButton("Debug meta", ToggleDebugMetadata));
        toolbar.Children.Add(ToolbarButton("Export PDF", ExportPdf));
    }

    public string RenderPreviewHtml(ManuscriptHostContext host)
    {
        var debug = host.Settings.Settings.BookAuthoring.DebugMetadata || (_currentBook?.DebugMode ?? false);
        return _previewRenderer.ToHtml(host.GetEditorText(), debug);
    }

    public void OnActivated(ManuscriptHostContext host)
    {
        _host = host;
        ReloadCatalog();
    }

    public void OnDeactivated(ManuscriptHostContext host) => _host = null;

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
            if (_chapterList is not null)
                _chapterList.ItemsSource = null;
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
        if (_host is null || _seriesCombo is null || _bookCombo is null || _chapterList is null)
            return;

        if (_seriesCombo.SelectedIndex < 0 || _bookCombo.SelectedIndex < 0)
            return;

        var series = _series[_seriesCombo.SelectedIndex];
        if (_bookCombo.SelectedIndex >= series.Books.Count)
            return;

        _currentBook = series.Books[_bookCombo.SelectedIndex];
        _host.Settings.Settings.BookAuthoring.BookId = _currentBook.Id;
        _host.Settings.Save();

        _chapterList.ItemsSource = _currentBook.Chapters
            .Select(c => $"{c.SortKey:0.###} — {c.Title}")
            .ToList();
    }

    private void OnChapterSelected()
    {
        if (_host is null || _chapterList is null || _currentBook is null || _chapterList.SelectedIndex < 0)
            return;

        if (_chapterList.SelectedIndex >= _currentBook.Chapters.Count)
            return;

        if (_host.Session.IsDirty)
            _host.Feedback.FlashWarning("Unsaved changes were discarded.");

        try
        {
            var chapter = _currentBook.Chapters[_chapterList.SelectedIndex];
            _host.Session.SelectFile(chapter.FilePath);
            _host.SetEditorText(_host.Session.EditorText);
            _host.RequestPreviewRefresh();
            _host.UpdateDirtyIndicator();
            _host.UpdateStatus();
            UpdateMetadataSummary();
        }
        catch (Exception ex)
        {
            _host.Feedback.FlashError(ex.Message);
        }
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
        _host.RequestPreviewRefresh();
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
        _host.RequestPreviewRefresh();
    }

    private void InsertThinking()
    {
        if (_host is null)
            return;

        var text = _host.GetEditorText();
        var updated = DialogueInsertHelper.InsertThinkingBlock(text, text.Length);
        _host.Session.EditorText = updated;
        _host.SetEditorText(updated);
        _host.RequestPreviewRefresh();
    }

    private void InsertChapterTag()
    {
        if (_host is null || _chapterList is null || _currentBook is null || _chapterList.SelectedIndex < 0)
        {
            _host?.Feedback.FlashWarning("Select a chapter first.");
            return;
        }

        var chapter = _currentBook.Chapters[_chapterList.SelectedIndex];
        var text = _host.GetEditorText();
        var updated = MetadataInsertHelper.InsertChapterTag(text, 0, chapter.SortKey);
        _host.Session.EditorText = updated;
        _host.SetEditorText(updated);
        _host.RequestPreviewRefresh();
    }

    private void ToggleDebugMetadata()
    {
        if (_host is null)
            return;

        _host.Settings.Settings.BookAuthoring.DebugMetadata = !_host.Settings.Settings.BookAuthoring.DebugMetadata;
        _host.Settings.Save();
        _host.RequestPreviewRefresh();
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
