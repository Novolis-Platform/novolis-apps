using Novolis.IO.Recovery;
using Novolis.IO.Watching;
using Novolis.Manuscript;

namespace BooksWriterStudio.Services;

internal sealed class WriterSession : IDisposable
{
    readonly HashSet<string> _dirtyPaths = new(StringComparer.OrdinalIgnoreCase);

    public ManuscriptWorkspace? Workspace { get; private set; }
    public IReadOnlyList<SeriesInfo> Series { get; private set; } = [];
    public IReadOnlyList<BookInfo> StandaloneBooks { get; private set; } = [];
    public SeriesInfo? SelectedSeries { get; private set; }
    public BookInfo? SelectedBook { get; private set; }
    public ChapterInfo? SelectedChapter { get; private set; }

    public string EditorText { get; set; } = string.Empty;
    public string LoadedSnapshot { get; private set; } = string.Empty;

    public ContentRecoveryStore? RecoveryStore { get; private set; }
    public DebouncedFileWatcher FileWatcher { get; } = new(400);

    public bool IsDirty =>
        SelectedChapter is not null
        && !string.Equals(EditorText, LoadedSnapshot, StringComparison.Ordinal);

    public bool IsChapterDirty(string filePath) => _dirtyPaths.Contains(filePath);

    public void OpenWorkspace(ManuscriptWorkspace workspace)
    {
        Workspace = workspace;
        Series = workspace.Catalog.Load(workspace.ContentRoot);
        StandaloneBooks = workspace.Catalog.LoadStandaloneBooks(workspace.ContentRoot);
        RecoveryStore = new ContentRecoveryStore(Path.Combine(workspace.ContentRoot, ".writer", "recovery"));
        SelectedSeries = null;
        SelectedBook = null;
        SelectedChapter = null;
        EditorText = string.Empty;
        LoadedSnapshot = string.Empty;
        _dirtyPaths.Clear();
        FileWatcher.Stop();
    }

    public void SelectSeries(SeriesInfo? series)
    {
        SelectedSeries = series;
        SelectedBook = series?.Books.FirstOrDefault();
        SelectedChapter = SelectedBook?.Chapters.FirstOrDefault();
    }

    public void SelectStandaloneBook(BookInfo book)
    {
        SelectedSeries = null;
        SelectedBook = book;
        SelectedChapter = book.Chapters.FirstOrDefault();
    }

    public void SelectBook(BookInfo? book)
    {
        SelectedBook = book;
        SelectedChapter = book?.Chapters.FirstOrDefault();
    }

    public void SelectChapter(ChapterInfo chapter)
    {
        SelectedChapter = chapter;
    }

    public void LoadChapterText(string text)
    {
        EditorText = text;
        LoadedSnapshot = text;
        if (SelectedChapter is not null)
            _dirtyPaths.Remove(SelectedChapter.FilePath);
    }

    public void MarkDirty()
    {
        if (SelectedChapter is null)
            return;

        if (IsDirty)
            _dirtyPaths.Add(SelectedChapter.FilePath);
        else
            _dirtyPaths.Remove(SelectedChapter.FilePath);
    }

    public void SaveCurrent()
    {
        if (SelectedChapter is null)
            throw new InvalidOperationException("No chapter selected.");

        File.WriteAllText(SelectedChapter.FilePath, EditorText);
        LoadedSnapshot = EditorText;
        _dirtyPaths.Remove(SelectedChapter.FilePath);
        RecoveryStore?.Clear(SelectedChapter.FilePath);
    }

    public string WriterRoot =>
        Workspace?.ContentRoot ?? Environment.CurrentDirectory;

    public string PrintSettingsPath =>
        Path.Combine(WriterRoot, ".writer", "print-settings.json");

    public string VoiceMapPath
    {
        get
        {
            var writerMap = Path.Combine(WriterRoot, ".writer", "voice-map.yaml");
            if (File.Exists(writerMap))
                return writerMap;

            var toolsMap = Path.Combine(WriterRoot, "tools", "audio", "voice-map.yaml");
            return File.Exists(toolsMap) ? toolsMap : writerMap;
        }
    }

    public void Dispose() => FileWatcher.Dispose();
}
