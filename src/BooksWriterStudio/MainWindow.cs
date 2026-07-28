using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit;
using BooksWriterStudio.Services;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Audio.Voice.EdgeTts;
using Novolis.Audio.Voice.Manuscript;
using Novolis.Avalonia.Controls;
using Novolis.Avalonia.Markdown;
using Novolis.Avalonia.Studio;
using Novolis.IO.Git;
using Novolis.IO.Recovery;
using Novolis.Markup.Manuscript;

namespace BooksWriterStudio;

internal sealed class MainWindow : Window
{
    const int AutosaveIdleMs = 4000;

    readonly WriterSession _session;
    readonly WriterSettingsStore _settings;
    readonly ManuscriptSpeechPreview _speechPreview;
    readonly SpellService _spell;
    readonly PublishJobQueue _jobs = new();
    readonly GitRepositoryService _git = new();

    readonly ComboBox _seriesCombo = new() { MinWidth = 140, Margin = new Thickness(0, 0, 6, 0), PlaceholderText = "Series" };
    readonly ComboBox _bookCombo = new() { MinWidth = 160, Margin = new Thickness(0, 0, 6, 0), PlaceholderText = "Book" };
    readonly ListBox _chapterList = new();
    readonly MarkdownSourceEditor _editor = new()
    {
        HighlightingProfile = MarkdownSourceHighlightingProfile.BookAuthoring,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
    };
    readonly TabControl _contextTabs = new();
    readonly JobQueuePanel _jobPanel = new();
    readonly Border _topBar = new();

    readonly TextBox _metaNumber = new();
    readonly TextBox _metaTitle = new();
    readonly TextBox _metaDate = new();
    readonly TextBox _metaTime = new();
    readonly TextBox _metaSystem = new();
    readonly TextBox _metaLocation = new();
    readonly TextBox _metaPov = new();
    readonly TextBox _metaCharacters = new();
    readonly TextBox _metaStatus = new();
    readonly TextBox _metaNotes = new();

    readonly TextBox _searchQuery = new() { PlaceholderText = "Search chapter text…" };
    readonly ListBox _searchResults = new();
    readonly ListBox _diagnosticsList = new();
    readonly TextBlock _scmStatus = new() { TextWrapping = TextWrapping.Wrap };
    readonly TextBox _printPageWidth = new();
    readonly TextBox _printPageHeight = new();
    readonly TextBox _printBodySize = new();
    readonly CheckBox _printIncludeCover = new() { Content = "Include cover" };
    readonly ComboBox _voiceCombo = new() { MinWidth = 180 };
    readonly ComboBox _voiceProfileCombo = new() { MinWidth = 160 };
    readonly TextBox _voiceRate = new() { PlaceholderText = "-4" };
    readonly TextBox _voicePitch = new() { PlaceholderText = "0" };
    readonly TextBox _voiceVolume = new() { PlaceholderText = "0" };

    StudioFeedback _feedback = null!;
    Grid _bodyGrid = null!;
    ManuscriptVoiceSettings _voiceSettings = new();
    ManuscriptPrintSettings _printSettings = new();
    DispatcherTimer? _autosaveTimer;
    bool _focusMode;
    bool _suppressEditorSync;
    bool _suppressChapterSelection;
    bool _suppressCatalogSelection;
    bool _suppressVoiceUi;
    bool _handlingExternalChange;

    public MainWindow(
        WriterSession session,
        WriterSettingsStore settings,
        ManuscriptSpeechPreview speechPreview,
        SpellService spell)
    {
        _session = session;
        _settings = settings;
        _speechPreview = speechPreview;
        _spell = spell;

        Title = "Books Writer Studio";
        Width = 1480;
        Height = 920;

        Content = BuildLayout();
        Opened += OnOpened;
        Closing += OnClosing;
        KeyDown += OnKeyDown;

        _editor.TextChanged += OnEditorTextChanged;
        _seriesCombo.SelectionChanged += (_, _) => OnSeriesChanged();
        _bookCombo.SelectionChanged += (_, _) => OnBookChanged();
        _chapterList.SelectionChanged += (_, _) => OnChapterSelectionChanged();
        _voiceProfileCombo.SelectionChanged += (_, _) => OnVoiceProfileChanged();
        _jobs.Changed += () => Dispatcher.UIThread.Post(RefreshJobPanel);
        _session.FileWatcher.FileChanged += OnExternalFileChanged;

        _voiceCombo.ItemsSource = EdgeVoiceCatalog.All.ToList();
        _voiceCombo.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(EdgeVoiceEntry.DisplayName));
        _voiceProfileCombo.ItemsSource = EdgeVoiceProfiles.All.ToList();
        _voiceProfileCombo.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(EdgeVoiceProfile.DisplayName));
    }

    Control BuildLayout()
    {
        var chrome = StudioChrome.Create();
        _feedback = chrome.CreateFeedback();

        // Single toolbar: workspace actions + catalog (no duplicate Save/Speak bars).
        _topBar.Background = new SolidColorBrush(Color.Parse("#252526"));
        _topBar.Padding = new Thickness(10, 6);
        _topBar.BorderBrush = new SolidColorBrush(Color.Parse("#3F3F46"));
        _topBar.BorderThickness = new Thickness(0, 0, 0, 1);
        var topInner = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        topInner.Children.Add(Button("Open…", OnOpenFolder));
        topInner.Children.Add(Button("Save", OnSave, "Ctrl+S"));
        topInner.Children.Add(Button("Speak", OnSpeakSelection, "Ctrl+Shift+Space"));
        topInner.Children.Add(Button("Stop", OnStopSpeech));
        topInner.Children.Add(Button("Spell", OnCheckSpelling));
        topInner.Children.Add(new Border
        {
            Width = 1,
            Margin = new Thickness(6, 2),
            Background = new SolidColorBrush(Color.Parse("#3F3F46")),
        });
        topInner.Children.Add(_seriesCombo);
        topInner.Children.Add(_bookCombo);
        topInner.Children.Add(Button("Refresh", OnRefreshCatalog));
        _topBar.Child = topInner;

        BuildContextTabs();

        var editorDock = new DockPanel();
        editorDock.Children.Add(_editor);

        var navTitle = new TextBlock { Text = "Chapters", FontWeight = FontWeight.Bold, Margin = new Thickness(8, 8, 8, 4) };
        var navDock = new DockPanel();
        DockPanel.SetDock(navTitle, Dock.Top);
        navDock.Children.Add(navTitle);
        navDock.Children.Add(_chapterList);
        var navBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#3F3F46")),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = navDock,
        };

        var contextBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#3F3F46")),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Child = _contextTabs,
            MinWidth = 360,
        };

        _bodyGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(
                $"{_settings.Settings.NavColumnWidth},*,{_settings.Settings.ContextColumnWidth}"),
        };
        Grid.SetColumn(navBorder, 0);
        Grid.SetColumn(editorDock, 1);
        Grid.SetColumn(contextBorder, 2);
        _bodyGrid.Children.Add(navBorder);
        _bodyGrid.Children.Add(editorDock);
        _bodyGrid.Children.Add(contextBorder);

        var flashStatus = new DockPanel();
        DockPanel.SetDock(chrome.FlashLine, Dock.Bottom);
        DockPanel.SetDock(chrome.StatusLine, Dock.Bottom);
        flashStatus.Children.Add(chrome.FlashLine);
        flashStatus.Children.Add(chrome.StatusLine);

        var workspaceHost = new Grid();
        workspaceHost.Children.Add(_bodyGrid);
        workspaceHost.Children.Add(chrome.BusyOverlay);

        var root = new DockPanel();
        DockPanel.SetDock(_topBar, Dock.Top);
        DockPanel.SetDock(flashStatus, Dock.Bottom);
        root.Children.Add(_topBar);
        root.Children.Add(flashStatus);
        root.Children.Add(workspaceHost);
        return root;
    }

    void BuildContextTabs()
    {
        _contextTabs.Items.Add(new TabItem { Header = "Metadata", Content = BuildMetadataPanel() });
        _contextTabs.Items.Add(new TabItem { Header = "Search", Content = BuildSearchPanel() });
        _contextTabs.Items.Add(new TabItem { Header = "Diagnostics", Content = BuildDiagnosticsPanel() });
        _contextTabs.Items.Add(new TabItem { Header = "Publish", Content = BuildPublishPanel() });
        _contextTabs.Items.Add(new TabItem { Header = "SCM", Content = BuildScmPanel() });
    }

    Control BuildMetadataPanel()
    {
        var panel = new StackPanel { Margin = new Thickness(8), Spacing = 6 };
        panel.Children.Add(Labeled("Number", _metaNumber));
        panel.Children.Add(Labeled("Title", _metaTitle));
        panel.Children.Add(Labeled("Date", _metaDate));
        panel.Children.Add(Labeled("Time", _metaTime));
        panel.Children.Add(Labeled("System", _metaSystem));
        panel.Children.Add(Labeled("Location", _metaLocation));
        panel.Children.Add(Labeled("POV", _metaPov));
        panel.Children.Add(Labeled("Characters", _metaCharacters));
        panel.Children.Add(Labeled("Status", _metaStatus));
        panel.Children.Add(Labeled("Notes", _metaNotes));
        var apply = Button("Apply to chapter", OnApplyMetadata);
        apply.Margin = new Thickness(0, 8, 0, 0);
        panel.Children.Add(apply);
        return new ScrollViewer { Content = panel };
    }

    Control BuildSearchPanel()
    {
        var panel = new DockPanel { Margin = new Thickness(8) };
        var searchBar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 0, 0, 8) };
        searchBar.Children.Add(_searchQuery);
        searchBar.Children.Add(Button("Search", OnSearch));
        DockPanel.SetDock(searchBar, Dock.Top);
        panel.Children.Add(searchBar);
        panel.Children.Add(_searchResults);
        _searchResults.DoubleTapped += (_, _) =>
        {
            if (_searchResults.SelectedItem is SearchHit hit)
                _ = LoadChapterByPathAsync(hit.FilePath);
        };
        return panel;
    }

    Control BuildDiagnosticsPanel()
    {
        var panel = new DockPanel { Margin = new Thickness(8) };
        var refresh = Button("Run doctor", OnRunDiagnostics);
        refresh.Margin = new Thickness(0, 0, 0, 8);
        DockPanel.SetDock(refresh, Dock.Top);
        panel.Children.Add(refresh);
        panel.Children.Add(_diagnosticsList);
        return panel;
    }

    Control BuildPublishPanel()
    {
        // Roomier layout: actions row, settings scroll, jobs fill remaining height.
        var exportRow = new WrapPanel
        {
            Margin = new Thickness(0, 0, 0, 12),
        };
        foreach (var btn in new[]
                 {
                     Button("Book PDF", OnExportBookPdf),
                     Button("Reference PDF", OnExportReferencePdf),
                     Button("Audiobook (MP3 + M4B)", OnGenerateAudiobook),
                 })
        {
            btn.Margin = new Thickness(0, 0, 8, 8);
            btn.MinWidth = 140;
            btn.Padding = new Thickness(12, 8);
            exportRow.Children.Add(btn);
        }

        var printPanel = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 12),
            Children =
            {
                new TextBlock { Text = "Print", FontWeight = FontWeight.SemiBold },
                Labeled("Width (in)", _printPageWidth),
                Labeled("Height (in)", _printPageHeight),
                Labeled("Body size", _printBodySize),
                _printIncludeCover,
                Button("Save print settings", OnSavePrintSettings),
            },
        };

        var voicePanel = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 12),
            Children =
            {
                new TextBlock { Text = "Voice", FontWeight = FontWeight.SemiBold },
                Labeled("Profile", _voiceProfileCombo),
                Labeled("Voice", _voiceCombo),
                Labeled("Rate (%)", _voiceRate),
                Labeled("Pitch (Hz)", _voicePitch),
                Labeled("Volume (%)", _voiceVolume),
                Button("Save voice settings", OnSaveVoiceSettings),
            },
        };

        var settingsTabs = new TabControl
        {
            Margin = new Thickness(0, 0, 0, 8),
            MinHeight = 220,
        };
        settingsTabs.Items.Add(new TabItem { Header = "Print", Content = new ScrollViewer { Content = printPanel } });
        settingsTabs.Items.Add(new TabItem { Header = "Voice", Content = new ScrollViewer { Content = voicePanel } });

        var jobsHeader = new TextBlock
        {
            Text = "Jobs",
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 6),
        };

        _jobPanel.MinHeight = 180;
        _jobPanel.VerticalAlignment = VerticalAlignment.Stretch;

        var jobsDock = new DockPanel();
        DockPanel.SetDock(jobsHeader, Dock.Top);
        jobsDock.Children.Add(jobsHeader);
        jobsDock.Children.Add(_jobPanel);

        var root = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(exportRow, Dock.Top);
        DockPanel.SetDock(settingsTabs, Dock.Top);
        root.Children.Add(exportRow);
        root.Children.Add(settingsTabs);
        root.Children.Add(jobsDock);

        _jobPanel.CancelRequested += row =>
        {
            if (row.Tag is PublishJob job)
                _jobs.Cancel(job);
        };
        _jobPanel.OpenOutputRequested += row =>
        {
            if (row.Tag is PublishJob { OutputPath: { } path } && File.Exists(path))
                ShellOpen(path);
        };
        return root;
    }

    Control BuildScmPanel()
    {
        var panel = new StackPanel { Margin = new Thickness(8), Spacing = 8 };
        panel.Children.Add(_scmStatus);
        panel.Children.Add(Button("Refresh git status", OnRefreshGitStatus));
        panel.Children.Add(Button("Checkpoint commit", OnCheckpoint));
        return panel;
    }

    async void OnOpened(object? sender, EventArgs e)
    {
        _settings.Load();
        ApplyEditorSettings();
        ApplyTheme();

        _autosaveTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(AutosaveIdleMs), DispatcherPriority.Background, (_, _) => TryAutosave());
        _autosaveTimer.Start();

        _spell.TryLoad(_settings.Settings.CustomDictionaryPath);

        var startup = Program.StartupFolder ?? _settings.Settings.ContentRoot ?? Environment.CurrentDirectory;
        if (ManuscriptWorkspace.TryOpen(startup, out var workspace) && workspace is not null)
            await OpenWorkspaceAsync(workspace.ContentRoot, restoreSelection: true);
        else if (!string.IsNullOrWhiteSpace(_settings.Settings.ContentRoot))
            await TryOpenPathAsync(_settings.Settings.ContentRoot, restoreSelection: true);

        UpdateStatus();
    }

    void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_session.IsDirty)
        {
            _feedback.FlashWarning("Unsaved changes in current chapter.");
        }

        _settings.Save();
        if (_session.Workspace is not null)
            _settings.SaveWorkspaceOverlay(_session.Workspace.ContentRoot);
    }

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFocusMode();
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.S)
        {
            OnSave(null!, null!);
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.P)
        {
            _ = GoToChapterAsync();
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.Space)
        {
            OnSpeakSelection(null!, null!);
            e.Handled = true;
        }
    }

    async void OnOpenFolder(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open books workspace",
            AllowMultiple = false,
        });
        if (folders.Count == 0)
            return;

        var path = folders[0].Path.LocalPath;
        await TryOpenPathAsync(path, restoreSelection: false);
    }

    async Task TryOpenPathAsync(string path, bool restoreSelection)
    {
        if (!ManuscriptWorkspace.TryOpen(path, out var workspace) || workspace is null)
        {
            _feedback.FlashError("Not a books workspace (expected content/series or content/books).");
            return;
        }

        await OpenWorkspaceAsync(workspace.ContentRoot, restoreSelection);
    }

    async Task OpenWorkspaceAsync(string contentRoot, bool restoreSelection)
    {
        if (_session.IsDirty)
            await SaveCurrentIfNeededAsync();

        if (!ManuscriptWorkspace.TryOpen(contentRoot, out var workspace) || workspace is null)
            return;

        _session.OpenWorkspace(workspace);
        _settings.Settings.ContentRoot = workspace.ContentRoot;
        _settings.LoadWorkspaceOverlay(workspace.ContentRoot);
        _settings.Save();

        LoadVoiceAndPrintSettings();
        BindCatalogCombos(restoreSelection);
        RefreshChapterList();
        RefreshScmStatus();
        _feedback.Flash($"Opened {workspace.ContentRoot}");

        if (_session.SelectedChapter is not null)
            await LoadChapterAsync(_session.SelectedChapter, skipSavePrompt: true);
        else
            UpdateStatus();
    }

    void BindCatalogCombos(bool restoreSelection)
    {
        _suppressCatalogSelection = true;
        try
        {
            // Titles only — drive session from computed indices (Avalonia may not
            // apply SelectedIndex synchronously right after ItemsSource assignment).
            var seriesTitles = new List<string> { "(standalone books)" };
            seriesTitles.AddRange(_session.Series.Select(s => string.IsNullOrWhiteSpace(s.Title) ? s.Id : s.Title));
            _seriesCombo.ItemsSource = seriesTitles;

            var seriesIndex = 0;
            if (restoreSelection && !string.IsNullOrWhiteSpace(_settings.Settings.LastSeriesId))
            {
                var idx = _session.Series.ToList().FindIndex(s =>
                    s.Id.Equals(_settings.Settings.LastSeriesId, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                    seriesIndex = idx + 1;
            }
            else if (_session.Series.Count > 0)
            {
                seriesIndex = 1;
            }

            ApplySeriesSelectionByIndex(seriesIndex, restoreSelection ? _settings.Settings.LastBookId : null);
            SelectComboItem(_seriesCombo, seriesTitles, seriesIndex);
        }
        finally
        {
            _suppressCatalogSelection = false;
        }
    }

    void BindBookCombo(string? restoreBookId)
    {
        var outer = _suppressCatalogSelection;
        _suppressCatalogSelection = true;
        try
        {
            var books = GetBooksForCurrentSeries();
            var bookTitles = books
                .Select(b => string.IsNullOrWhiteSpace(b.Title) ? b.Id : b.Title)
                .ToList();
            _bookCombo.ItemsSource = bookTitles;

            var bookIndex = -1;
            if (!string.IsNullOrWhiteSpace(restoreBookId))
            {
                bookIndex = books.FindIndex(b =>
                    b.Id.Equals(restoreBookId, StringComparison.OrdinalIgnoreCase));
            }

            if (bookIndex < 0 && books.Count > 0)
                bookIndex = 0;

            ApplyBookSelectionByIndex(bookIndex);
            SelectComboItem(_bookCombo, bookTitles, bookIndex);
        }
        finally
        {
            _suppressCatalogSelection = outer;
        }
    }

    static void SelectComboItem(ComboBox combo, IReadOnlyList<string> items, int index)
    {
        if (index < 0 || index >= items.Count)
        {
            combo.SelectedIndex = -1;
            combo.SelectedItem = null;
            return;
        }

        // Prefer SelectedItem so the display text sticks even when SelectedIndex lags.
        combo.SelectedItem = items[index];
        if (combo.SelectedIndex != index)
            combo.SelectedIndex = index;
    }

    List<BookInfo> GetBooksForCurrentSeries()
    {
        if (_session.SelectedSeries is not null)
            return _session.SelectedSeries.Books.ToList();
        return _session.StandaloneBooks.ToList();
    }

    void ApplySeriesSelectionByIndex(int index, string? restoreBookId)
    {
        if (index <= 0)
        {
            _session.SelectSeries(null);
            _settings.Settings.LastSeriesId = null;
        }
        else
        {
            var seriesIdx = index - 1;
            if (seriesIdx < 0 || seriesIdx >= _session.Series.Count)
            {
                _session.SelectSeries(null);
                _settings.Settings.LastSeriesId = null;
            }
            else
            {
                var series = _session.Series[seriesIdx];
                _session.SelectSeries(series);
                _settings.Settings.LastSeriesId = series.Id;
            }
        }

        BindBookCombo(restoreBookId);
    }

    void ApplyBookSelectionByIndex(int index)
    {
        var books = GetBooksForCurrentSeries();
        if (index < 0 || index >= books.Count)
        {
            _session.SelectBook(null);
            _settings.Settings.LastBookId = null;
            return;
        }

        var book = books[index];
        if (_session.SelectedSeries is null)
            _session.SelectStandaloneBook(book);
        else
            _session.SelectBook(book);

        _settings.Settings.LastBookId = book.Id;
    }

    void OnSeriesChanged()
    {
        if (_suppressCatalogSelection)
            return;
        if (_seriesCombo.SelectedIndex < 0)
            return;

        ApplySeriesSelectionByIndex(_seriesCombo.SelectedIndex, restoreBookId: null);
        RefreshChapterList();
        _ = SelectFirstChapterAsync();
    }

    void OnBookChanged()
    {
        if (_suppressCatalogSelection)
            return;
        if (_bookCombo.SelectedIndex < 0)
            return;

        ApplyBookSelectionByIndex(_bookCombo.SelectedIndex);
        _settings.Settings.LastSeriesId = _session.SelectedSeries?.Id;
        RefreshChapterList();
        _ = SelectFirstChapterAsync();
    }

    async Task SelectFirstChapterAsync()
    {
        if (_session.SelectedChapter is null)
        {
            _editor.Text = string.Empty;
            UpdateStatus();
            return;
        }

        await LoadChapterAsync(_session.SelectedChapter, skipSavePrompt: false);
    }

    async void OnChapterSelectionChanged()
    {
        if (_suppressChapterSelection)
            return;
        if (_chapterList.SelectedItem is not ListBoxItem { Tag: MarkedListRow row }
            || row.Tag is not ChapterInfo chapter)
            return;

        await LoadChapterAsync(chapter, skipSavePrompt: false);
    }

    async Task LoadChapterAsync(ChapterInfo chapter, bool skipSavePrompt)
    {
        if (!skipSavePrompt && _session.IsDirty)
        {
            if (!await SaveCurrentIfNeededAsync())
                return;
        }

        _session.SelectChapter(chapter);
        _settings.Settings.LastChapterId = chapter.Id;
        _session.FileWatcher.Stop();

        var diskText = File.ReadAllText(chapter.FilePath);
        var recovery = _session.RecoveryStore?.GetLatest(chapter.FilePath);
        if (recovery is not null && !string.Equals(recovery.Content, diskText, StringComparison.Ordinal))
        {
            var choice = await ChoiceDialog.ShowAsync(
                this,
                "Recovery available",
                "A recovery snapshot differs from the file on disk.",
                $"{Path.GetFileName(chapter.FilePath)} · {recovery.TimestampUtc:g} UTC",
                [
                    new ChoiceOption("restore", "Restore recovery"),
                    new ChoiceOption("discard", "Discard recovery"),
                    new ChoiceOption("keep", "Keep file", IsDefault: true, IsCancel: true),
                ]);

            if (choice == "restore")
                diskText = recovery.Content;
            else if (choice == "discard")
                _session.RecoveryStore?.Clear(chapter.FilePath);
        }

        _suppressEditorSync = true;
        _session.LoadChapterText(diskText);
        _editor.Text = diskText;
        _suppressEditorSync = false;

        LoadMetadataFromEditor();
        _session.FileWatcher.Watch(chapter.FilePath);
        UpdateChapterListMarkers();
        UpdateStatus();
        _editor.FocusEditor();
    }

    async Task LoadChapterByPathAsync(string filePath)
    {
        var book = _session.SelectedBook;
        var chapter = book?.Chapters.FirstOrDefault(c =>
            c.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
        if (chapter is null)
            return;

        SelectChapterInList(chapter);
        await LoadChapterAsync(chapter, skipSavePrompt: false);
    }

    void SelectChapterInList(ChapterInfo chapter)
    {
        _suppressChapterSelection = true;
        try
        {
            foreach (var item in _chapterList.Items)
            {
                if (item is ListBoxItem { Tag: MarkedListRow row } && row.Tag is ChapterInfo c && c.Id == chapter.Id)
                {
                    _chapterList.SelectedItem = item;
                    break;
                }
            }
        }
        finally
        {
            _suppressChapterSelection = false;
        }
    }

    async Task<bool> SaveCurrentIfNeededAsync()
    {
        if (!_session.IsDirty)
            return true;

        var choice = await ChoiceDialog.ShowAsync(
            this,
            "Unsaved changes",
            "Save changes before switching chapters?",
            _session.SelectedChapter?.FilePath,
            [
                new ChoiceOption("save", "Save", IsDefault: true),
                new ChoiceOption("discard", "Discard"),
                new ChoiceOption("cancel", "Cancel", IsCancel: true),
            ]);

        if (choice == "cancel")
            return false;
        if (choice == "discard")
            return true;

        OnSave(null!, null!);
        return true;
    }

    void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_suppressEditorSync)
            return;

        _session.EditorText = _editor.Text ?? string.Empty;
        _session.MarkDirty();
        RefreshChapterList();
        UpdateStatus();
        RestartAutosaveTimer();
    }

    void RestartAutosaveTimer()
    {
        _autosaveTimer?.Stop();
        _autosaveTimer?.Start();
    }

    void TryAutosave()
    {
        if (!_session.IsDirty || _session.SelectedChapter is null || _session.RecoveryStore is null)
            return;

        _session.RecoveryStore.WriteSnapshot(_session.SelectedChapter.FilePath, _session.EditorText);
    }

    void OnSave(object? sender, RoutedEventArgs e)
    {
        if (_session.SelectedChapter is null)
        {
            _feedback.FlashWarning("No chapter selected.");
            return;
        }

        try
        {
            _session.EditorText = _editor.Text ?? string.Empty;
            _session.SaveCurrent();
            RefreshChapterList();
            UpdateStatus();
            _feedback.Flash("Saved.");
        }
        catch (Exception ex)
        {
            _feedback.FlashError(ex.Message);
        }
    }

    async void OnExternalFileChanged(string path)
    {
        if (_handlingExternalChange || _session.SelectedChapter?.FilePath != path)
            return;

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (_handlingExternalChange || _session.SelectedChapter?.FilePath != path)
                return;

            _handlingExternalChange = true;
            try
            {
                var choice = await ChoiceDialog.ShowAsync(
                    this,
                    "External change",
                    "The file changed on disk while you were editing.",
                    Path.GetFileName(path),
                    [
                        new ChoiceOption("keep", "Keep local", IsDefault: true),
                        new ChoiceOption("reload", "Reload disk"),
                        new ChoiceOption("compare", "Compare later", IsCancel: true),
                    ]);

                if (choice == "reload")
                {
                    _suppressEditorSync = true;
                    var text = File.ReadAllText(path);
                    _session.LoadChapterText(text);
                    _editor.Text = text;
                    _suppressEditorSync = false;
                    LoadMetadataFromEditor();
                    UpdateChapterListMarkers();
                    UpdateStatus();
                    _feedback.Flash("Reloaded from disk.");
                }
            }
            finally
            {
                _handlingExternalChange = false;
            }
        });
    }

    void LoadMetadataFromEditor()
    {
        var (meta, _, _) = ManuscriptMetadata.Parse(_editor.Text ?? string.Empty);
        _metaNumber.Text = meta.Number ?? string.Empty;
        _metaTitle.Text = meta.Title ?? string.Empty;
        _metaDate.Text = meta.Date ?? string.Empty;
        _metaTime.Text = meta.Time ?? string.Empty;
        _metaSystem.Text = meta.System ?? string.Empty;
        _metaLocation.Text = meta.Location ?? string.Empty;
        _metaPov.Text = meta.Pov ?? string.Empty;
        _metaCharacters.Text = meta.Characters ?? string.Empty;
        _metaStatus.Text = meta.Status ?? string.Empty;
        _metaNotes.Text = meta.Notes ?? string.Empty;
    }

    void OnApplyMetadata(object? sender, RoutedEventArgs e)
    {
        var meta = new ManuscriptChapterMetadata
        {
            Number = NullIfEmpty(_metaNumber.Text),
            Title = NullIfEmpty(_metaTitle.Text),
            Date = NullIfEmpty(_metaDate.Text),
            Time = NullIfEmpty(_metaTime.Text),
            System = NullIfEmpty(_metaSystem.Text),
            Location = NullIfEmpty(_metaLocation.Text),
            Pov = NullIfEmpty(_metaPov.Text),
            Characters = NullIfEmpty(_metaCharacters.Text),
            Status = NullIfEmpty(_metaStatus.Text),
            Notes = NullIfEmpty(_metaNotes.Text),
        };

        var updated = ManuscriptMetadata.ApplyCallouts(_editor.Text ?? string.Empty, meta);
        _suppressEditorSync = true;
        _editor.Text = updated;
        _session.EditorText = updated;
        _suppressEditorSync = false;
        _session.MarkDirty();
        RefreshChapterList();
        UpdateStatus();
        _feedback.Flash("Metadata applied.");
    }

    void OnSearch(object? sender, RoutedEventArgs e)
    {
        _searchResults.Items.Clear();
        var query = _searchQuery.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query) || _session.SelectedBook is null)
            return;

        var hits = new List<SearchHit>();
        foreach (var chapter in _session.SelectedBook.Chapters)
        {
            var text = File.ReadAllText(chapter.FilePath);
            if (text.Contains(query, StringComparison.OrdinalIgnoreCase))
                hits.Add(new SearchHit(chapter.Title, chapter.FilePath, CountOccurrences(text, query)));
        }

        foreach (var hit in hits.OrderByDescending(h => h.Occurrences))
        {
            _searchResults.Items.Add(new ListBoxItem
            {
                Content = $"{hit.Title} ({hit.Occurrences})",
                Tag = hit,
            });
        }

        _feedback.Flash(hits.Count == 0 ? "No matches." : $"{hits.Count} chapter(s) matched.");
    }

    void OnRunDiagnostics(object? sender, RoutedEventArgs e)
    {
        _diagnosticsList.Items.Clear();
        if (_session.SelectedBook is null)
        {
            _feedback.FlashWarning("Select a book first.");
            return;
        }

        var findings = ManuscriptDoctor.Diagnose(_session.SelectedBook);
        foreach (var f in findings)
        {
            _diagnosticsList.Items.Add(new ListBoxItem
            {
                Content = $"[{f.Severity}] {f.Code}: {f.Message}",
                Tag = f,
            });
        }

        _feedback.Flash($"{findings.Count} finding(s).");
    }

    void OnExportBookPdf(object? sender, RoutedEventArgs e)
    {
        if (_session.SelectedBook is null)
        {
            _feedback.FlashWarning("Select a book first.");
            return;
        }

        var book = _session.SelectedBook;
        var outDir = Path.Combine(_session.WriterRoot, "out");
        Directory.CreateDirectory(outDir);
        var output = Path.Combine(outDir, $"{book.Id}.pdf");

        _jobs.Enqueue("Export book PDF", async (job, ct) =>
        {
            job.Detail = output;
            await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                ManuscriptBookPdfExporter.ExportBook(book, output, _printSettings);
            }, ct).ConfigureAwait(false);
            job.OutputPath = output;
            job.Log = $"Wrote {output}";
        });
        RefreshJobPanel();
        _feedback.Flash("PDF export queued.");
    }

    void OnExportReferencePdf(object? sender, RoutedEventArgs e)
    {
        var refs = _session.SelectedBook?.References ?? _session.SelectedSeries?.References ?? [];
        if (refs.Count == 0)
        {
            _feedback.FlashWarning("No reference sets found.");
            return;
        }

        var refSet = refs[0];
        var outDir = Path.Combine(_session.WriterRoot, "out");
        Directory.CreateDirectory(outDir);
        var output = Path.Combine(outDir, $"{refSet.Id}-references.pdf");

        _jobs.Enqueue("Export reference PDF", async (job, ct) =>
        {
            job.Detail = output;
            await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                ManuscriptBookPdfExporter.ExportReferenceSet(refSet, output, _printSettings);
            }, ct).ConfigureAwait(false);
            job.OutputPath = output;
            job.Log = $"Wrote {output}";
        });
        RefreshJobPanel();
        _feedback.Flash("Reference PDF export queued.");
    }

    void OnGenerateAudiobook(object? sender, RoutedEventArgs e)
    {
        if (_session.SelectedBook is null)
        {
            _feedback.FlashWarning("Select a book first.");
            return;
        }

        var book = _session.SelectedBook;
        var outDir = Path.Combine(_session.WriterRoot, "out", "audio", book.Id);
        var chapters = book.Chapters
            .Select(c => new AudiobookChapterInput(c.Id, c.Title, c.FilePath))
            .ToList();

        var synthesizer = Program.ApplicationHost.Services.GetRequiredService<EdgeTtsManuscriptSynthesizer>();
        var pipeline = new ManuscriptAudiobookPipeline(synthesizer);
        var options = new ManuscriptAudiobookOptions
        {
            OutputDirectory = outDir,
            AssembleMode = AudiobookAssembleMode.Both,
            ParallelJobs = 2,
        };

        _jobs.Enqueue("Generate audiobook", async (job, ct) =>
        {
            job.Detail = outDir;
            job.Progress = 0;
            job.ProgressLabel = $"0/{chapters.Count} chapters";
            job.ChapterProgress.Clear();
            foreach (var chapter in chapters)
            {
                job.ChapterProgress.Add(new PublishJobChapterProgress
                {
                    Label = chapter.Title,
                    Progress = 0,
                    StatusLabel = "pending",
                });
            }

            _jobs.NotifyProgress(force: true);

            var progress = new Progress<AudiobookProgress>(snapshot =>
            {
                void Apply()
                {
                    job.Progress = snapshot.OverallFraction;
                    job.ProgressLabel = snapshot.Message;
                    job.Detail = $"{outDir} · {snapshot.Message}";
                    job.Log = BuildAudiobookLog(snapshot);

                    for (var i = 0; i < snapshot.Chapters.Count && i < job.ChapterProgress.Count; i++)
                    {
                        var src = snapshot.Chapters[i];
                        var dst = job.ChapterProgress[i];
                        dst.Progress = src.Fraction;
                        dst.StatusLabel = src.StatusLabel;
                    }

                    _jobs.NotifyProgress();
                }

                if (Dispatcher.UIThread.CheckAccess())
                    Apply();
                else
                    Dispatcher.UIThread.Post(Apply);
            });

            var result = await pipeline.GenerateAsync(book.Id, chapters, _voiceSettings, options, progress, ct)
                .ConfigureAwait(false);
            job.OutputPath = result.ConcatenatedMp3Path ?? result.M4bPath ?? outDir;
            job.Progress = 1;
            job.ProgressLabel = $"Done — {result.Manifest.Chapters.Count} chapters";
            job.Detail = outDir;
            job.Log = $"Chapters: {result.Manifest.Chapters.Count}; manifest: {result.ManifestPath}";
            _jobs.NotifyProgress(force: true);
        });
        RefreshJobPanel();
        _feedback.Flash("Audiobook generation queued.");
    }

    static string BuildAudiobookLog(AudiobookProgress snapshot)
    {
        var lines = new List<string>
        {
            snapshot.Message,
            $"Overall: {snapshot.OverallFraction:P0} · phase: {snapshot.Phase}",
        };
        foreach (var chapter in snapshot.Chapters)
            lines.Add($"{chapter.Title}: {chapter.StatusLabel} ({chapter.Fraction:P0})");
        return string.Join(Environment.NewLine, lines);
    }

    void OnRefreshGitStatus(object? sender, RoutedEventArgs e) => RefreshScmStatus();

    void OnCheckpoint(object? sender, RoutedEventArgs e)
    {
        if (_session.Workspace is null)
            return;

        try
        {
            var result = _git.Checkpoint(_session.Workspace.ContentRoot, $"Books Writer checkpoint {DateTime.Now:yyyy-MM-dd HH:mm}");
            if (result.Ok)
            {
                _feedback.Flash("Checkpoint committed.");
                RefreshScmStatus();
            }
            else
            {
                _feedback.FlashError(result.Message);
            }
        }
        catch (Exception ex)
        {
            _feedback.FlashError(ex.Message);
        }
    }

    void RefreshScmStatus()
    {
        if (_session.Workspace is null)
        {
            _scmStatus.Text = "No workspace open.";
            return;
        }

        try
        {
            var status = _git.GetStatus(_session.Workspace.ContentRoot);
            _scmStatus.Text =
                $"Branch: {status.Branch}\n" +
                $"Dirty: {status.Dirty} ({status.DirtyFiles.Count} files)\n" +
                $"Ahead/behind: {status.Ahead}/{status.Behind}\n" +
                $"Last: {status.LastCommitSha} {status.LastCommitMessage}";
        }
        catch (Exception ex)
        {
            _scmStatus.Text = $"Git unavailable: {ex.Message}";
        }
    }

    void LoadVoiceAndPrintSettings()
    {
        _printSettings = ManuscriptPrintSettings.Load(_session.PrintSettingsPath);
        _printPageWidth.Text = _printSettings.PageWidthInches.ToString("0.##");
        _printPageHeight.Text = _printSettings.PageHeightInches.ToString("0.##");
        _printBodySize.Text = _printSettings.BodyFontSize.ToString("0.##");
        _printIncludeCover.IsChecked = _printSettings.IncludeCover;

        _voiceSettings = File.Exists(_session.VoiceMapPath)
            ? VoiceMapStore.Load(_session.VoiceMapPath)
            : ManuscriptVoiceSettings.FromProfile(EdgeVoiceProfiles.Narrator);

        ApplyVoiceSettingsToUi(_voiceSettings);
    }

    void ApplyVoiceSettingsToUi(ManuscriptVoiceSettings settings)
    {
        _suppressVoiceUi = true;
        try
        {
            EdgeVoiceEntry voiceEntry;
            try
            {
                voiceEntry = EdgeVoiceCatalog.Get(settings.Voice);
            }
            catch (ArgumentOutOfRangeException)
            {
                voiceEntry = EdgeVoiceCatalog.Get(EdgeVoice.EnUsAva);
            }

            // SelectedItem must match an ItemsSource entry for ComboBox display.
            _voiceCombo.SelectedItem = EdgeVoiceCatalog.All.First(v => v.Voice == voiceEntry.Voice);
            _voiceRate.Text = settings.Rate.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _voicePitch.Text = settings.Pitch.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _voiceVolume.Text = settings.Volume.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

            var profile = EdgeVoiceProfiles.All.FirstOrDefault(p =>
                p.Voice == settings.Voice
                && p.Rate.Value == settings.Rate.Value
                && p.Pitch.Value == settings.Pitch.Value
                && p.Volume.Value == settings.Volume.Value);
            _voiceProfileCombo.SelectedItem = profile;
        }
        finally
        {
            _suppressVoiceUi = false;
        }
    }

    void OnVoiceProfileChanged()
    {
        if (_suppressVoiceUi)
            return;
        if (_voiceProfileCombo.SelectedItem is not EdgeVoiceProfile profile)
            return;

        ApplyVoiceSettingsToUi(ManuscriptVoiceSettings.FromProfile(profile, _voiceSettings.Pronunciation));
    }

    void OnSavePrintSettings(object? sender, RoutedEventArgs e)
    {
        if (float.TryParse(_printPageWidth.Text, out var w))
            _printSettings.PageWidthInches = w;
        if (float.TryParse(_printPageHeight.Text, out var h))
            _printSettings.PageHeightInches = h;
        if (float.TryParse(_printBodySize.Text, out var body))
            _printSettings.BodyFontSize = body;
        _printSettings.IncludeCover = _printIncludeCover.IsChecked == true;
        _printSettings.Save(_session.PrintSettingsPath);
        _feedback.Flash("Print settings saved.");
    }

    void OnSaveVoiceSettings(object? sender, RoutedEventArgs e)
    {
        var voice = _voiceCombo.SelectedItem is EdgeVoiceEntry entry
            ? entry.Voice
            : _voiceSettings.Voice;

        if (!ProsodyPercent.TryParse(_voiceRate.Text, out var rate))
            rate = _voiceSettings.Rate;
        if (!ProsodyHertz.TryParse(_voicePitch.Text, out var pitch))
            pitch = _voiceSettings.Pitch;
        if (!ProsodyPercent.TryParse(_voiceVolume.Text, out var volume))
            volume = _voiceSettings.Volume;

        _voiceSettings = new ManuscriptVoiceSettings
        {
            Voice = voice,
            Rate = rate,
            Pitch = pitch,
            Volume = volume,
            SceneBreakMs = _voiceSettings.SceneBreakMs,
            PauseMs = _voiceSettings.PauseMs,
            MaxChunkChars = _voiceSettings.MaxChunkChars,
            Pronunciation = _voiceSettings.Pronunciation,
        };
        VoiceMapStore.Save(_session.VoiceMapPath, _voiceSettings);
        _feedback.Flash("Voice settings saved.");
    }

    async void OnSpeakSelection(object? sender, RoutedEventArgs e)
    {
        var selection = EditorSelectionHelper.GetSelectedText(_editor);
        if (string.IsNullOrWhiteSpace(selection))
        {
            _feedback.FlashWarning("Select text to speak.");
            return;
        }

        try
        {
            await _speechPreview.PreviewAsync(selection, _voiceSettings);
            _feedback.Flash("Speaking selection…");
        }
        catch (Exception ex)
        {
            _feedback.FlashError(ex.Message);
        }
    }

    void OnStopSpeech(object? sender, RoutedEventArgs e)
    {
        _speechPreview.Stop();
        _feedback.Flash("Speech stopped.");
    }

    void OnCheckSpelling(object? sender, RoutedEventArgs e)
    {
        if (!_spell.IsAvailable)
        {
            _feedback.FlashWarning("No Hunspell dictionary found (place en_US.aff/dic under Dictionaries).");
            return;
        }

        var text = EditorSelectionHelper.GetSelectedText(_editor);
        if (string.IsNullOrWhiteSpace(text))
            text = _editor.Text ?? string.Empty;

        var misses = _spell.FindMisspellings(text);
        if (misses.Count == 0)
            _feedback.Flash("No misspellings found.");
        else
            _feedback.FlashWarning($"{misses.Count} misspelling(s): {string.Join(", ", misses.Take(12))}{(misses.Count > 12 ? "…" : "")}");
    }

    async Task GoToChapterAsync()
    {
        if (_session.SelectedBook is null)
            return;

        var picks = _session.SelectedBook.Chapters
            .Select(c => $"{c.Id} — {c.Title}")
            .ToList();
        var chosen = await FilteredPickerDialog<string>.ShowAsync(this, "Go To Chapter", picks, s => s);
        if (chosen is null)
            return;

        var id = chosen.Split('—', 2)[0].Trim();
        var chapter = _session.SelectedBook.Chapters.FirstOrDefault(c => c.Id == id);
        if (chapter is null)
            return;

        SelectChapterInList(chapter);
        await LoadChapterAsync(chapter, skipSavePrompt: false);
    }

    void OnRefreshCatalog(object? sender, RoutedEventArgs e)
    {
        if (_session.Workspace is null)
            return;

        _session.OpenWorkspace(_session.Workspace);
        BindCatalogCombos(restoreSelection: true);
        RefreshChapterList();
        _feedback.Flash("Catalog refreshed.");
    }

    void RefreshChapterList()
    {
        var book = _session.SelectedBook;
        _suppressChapterSelection = true;
        try
        {
            _chapterList.SelectedItem = null;
            _chapterList.Items.Clear();
            if (book is null)
                return;

            var rows = book.Chapters.Select((ch, i) =>
            {
                var words = ManuscriptMetadata.CountWords(File.Exists(ch.FilePath)
                    ? (_session.SelectedChapter?.FilePath == ch.FilePath ? _session.EditorText : File.ReadAllText(ch.FilePath))
                    : string.Empty);
                var dirty = _session.IsChapterDirty(ch.FilePath) ? "*" : null;
                return new MarkedListRow(dirty, (i + 1).ToString(), ch.Title, words.ToString(), ch);
            }).ToList();

            foreach (var row in rows)
            {
                _chapterList.Items.Add(new ListBoxItem
                {
                    Content = MarkedListBox.CreateItem(row),
                    Tag = row,
                });
            }

            if (_session.SelectedChapter is not null)
            {
                foreach (var item in _chapterList.Items)
                {
                    if (item is ListBoxItem { Tag: MarkedListRow row } && row.Tag is ChapterInfo c
                        && c.Id == _session.SelectedChapter.Id)
                    {
                        _chapterList.SelectedItem = item;
                        break;
                    }
                }
            }
        }
        finally
        {
            _suppressChapterSelection = false;
        }
    }

    void UpdateChapterListMarkers()
    {
        foreach (var raw in _chapterList.Items)
        {
            if (raw is not ListBoxItem item || item.Tag is not MarkedListRow row || row.Tag is not ChapterInfo ch)
                continue;

            var dirty = _session.IsChapterDirty(ch.FilePath) ? "*" : null;
            var words = ManuscriptMetadata.CountWords(
                _session.SelectedChapter?.FilePath == ch.FilePath
                    ? _session.EditorText
                    : File.Exists(ch.FilePath) ? File.ReadAllText(ch.FilePath) : string.Empty);
            var updated = new MarkedListRow(dirty, row.Leading, ch.Title, words.ToString(), ch);
            item.Tag = updated;
            item.Content = MarkedListBox.CreateItem(updated);
        }
    }

    void RefreshJobPanel()
    {
        var rows = _jobs.Jobs.Select(job => new JobQueueRow
        {
            Title = job.Title,
            StatusLabel = job.Status.ToString(),
            Detail = job.Detail,
            LogTail = job.Log ?? job.Detail,
            CanCancel = job.Status is PublishJobStatus.Queued or PublishJobStatus.Running,
            CanOpenOutput = job.Status == PublishJobStatus.Succeeded && !string.IsNullOrWhiteSpace(job.OutputPath),
            Progress = job.Progress,
            ProgressLabel = job.ProgressLabel,
            ChapterProgress = job.ChapterProgress.Count == 0
                ? null
                : job.ChapterProgress.Select(c => new JobChapterProgress
                {
                    Label = c.Label,
                    Progress = c.Progress,
                    StatusLabel = c.StatusLabel,
                }).Cast<IJobChapterProgress>().ToList(),
            Tag = job,
        }).Cast<IJobQueueRow>().ToList();

        _jobPanel.SetJobs(rows);
    }

    void UpdateStatus()
    {
        var chapterWords = ManuscriptMetadata.CountWords(_session.EditorText);
        var bookWords = _session.SelectedBook?.Chapters.Sum(ch =>
        {
            if (_session.SelectedChapter?.FilePath == ch.FilePath)
                return ManuscriptMetadata.CountWords(_session.EditorText);
            return File.Exists(ch.FilePath)
                ? ManuscriptMetadata.CountWords(File.ReadAllText(ch.FilePath))
                : 0;
        }) ?? 0;

        var path = _session.SelectedChapter?.FilePath ?? "(no chapter)";
        var dirty = _session.IsDirty ? " *" : string.Empty;
        var text = $"{path}{dirty}  ·  {chapterWords} words  ·  book {bookWords} words";
        _feedback.SetStatus(text);
        _topBar.BorderBrush = _session.IsDirty
            ? StudioStatusBrushes.Dirty
            : new SolidColorBrush(Color.Parse("#3F3F46"));
    }

    void ApplyEditorSettings()
    {
        _editor.BaseFontSize = _settings.Settings.EditorFontSize;
        _editor.ZoomScale = _settings.Settings.EditorZoom;
    }

    void ApplyTheme()
    {
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant =
                _settings.Settings.Theme.Equals("Light", StringComparison.OrdinalIgnoreCase)
                    ? ThemeVariant.Light
                    : ThemeVariant.Dark;
        }
    }

    void ToggleFocusMode()
    {
        _focusMode = !_focusMode;
        StudioFocusMode.Apply(_focusMode, _topBar, _contextTabs);
        _feedback.Flash(_focusMode ? "Focus mode on" : "Focus mode off");
    }

    static Button Button(string label, EventHandler<RoutedEventArgs> click, string? tooltip = null)
    {
        var btn = new Button { Content = label };
        if (tooltip is not null)
            ToolTip.SetTip(btn, tooltip);
        btn.Click += click;
        return btn;
    }

    static Control Labeled(string label, Control input)
    {
        return new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock { Text = label, Opacity = 0.75, FontSize = 11 },
                input,
            },
        };
    }

    static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    static int CountOccurrences(string text, string query)
    {
        var count = 0;
        var idx = 0;
        while ((idx = text.IndexOf(query, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            idx += query.Length;
        }

        return count;
    }

    static void ShellOpen(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch
        {
            // Ignore shell open failures.
        }
    }

    sealed record SearchHit(string Title, string FilePath, int Occurrences);
}

internal static class EditorSelectionHelper
{
    static readonly FieldInfo? EditorField =
        typeof(MarkdownSourceEditor).GetField("_editor", BindingFlags.Instance | BindingFlags.NonPublic);

    public static string? GetSelectedText(MarkdownSourceEditor editor)
    {
        if (EditorField?.GetValue(editor) is not TextEditor textEditor)
            return null;

        return textEditor.SelectionLength > 0 ? textEditor.SelectedText : null;
    }
}
