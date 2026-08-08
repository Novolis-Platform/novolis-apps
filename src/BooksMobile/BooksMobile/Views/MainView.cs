using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using BooksMobile.Services;
using BooksMobile.Ui;
using Novolis.Avalonia.Layout;
using Novolis.Avalonia.Markdown;
using Novolis.Manuscript;

namespace BooksMobile.Views;

/// <summary>Narrow <see cref="AuthoringWorkspace"/> host: auth/library (Nav), book list (Context), chapter edit/listen (Primary).</summary>
public sealed class MainView : UserControl
{
    enum Screen
    {
        Auth,
        Library,
        Book,
        Chapter,
    }

    enum ChapterMode
    {
        Edit,
        Read,
    }

    readonly BooksMobileSession _session;
    readonly ChapterSpeechService _speech;
    readonly IScreenWakeLock _wakeLock;
    readonly AuthoringWorkspace _workspace;
    readonly Control _authPanel;
    readonly Control _chrome;
    readonly TextBlock _statusShell = BooksTheme.Muted(string.Empty, 13);
    readonly TextBlock _chromeTitle = BooksTheme.Body("Library", 16);
    readonly Button _backButton;
    readonly Button _menuButton;
    readonly TextBox _userCode;
    readonly Button _copyCode;
    readonly Button _signIn;
    readonly DockPanel _chapterHost;
    readonly Button _listenButton;
    readonly Button _undoButton;
    readonly Button _modeButton;
    readonly Button _prevChapterButton;
    readonly Button _nextChapterButton;
    readonly TextBlock _chapterMeta = BooksTheme.Muted(string.Empty, 12);
    readonly Border _editorHost;
    readonly MarkdownPreviewPane _preview;
    readonly DispatcherTimer _autoSaveTimer;

    MarkdownSourceEditor? _editor;
    IDisposable? _wake;
    Screen _screen = Screen.Auth;
    ChapterMode _chapterMode = ChapterMode.Edit;
    BookInfo? _openBook;
    string? _openSeriesTitle;
    string? _loadedPath;
    string? _baselineText;
    string? _lastClipboardCode;
    string? _chapterTitle;
    ChapterInfo? _openChapter;
    bool _suppressDirty;
    bool _busy;

    public MainView(BooksMobileSession session, ChapterSpeechService speech, IScreenWakeLock wakeLock)
    {
        _session = session;
        _speech = speech;
        _wakeLock = wakeLock;
        Background = BooksPalette.WindowBrush;
        Focusable = true;

        _signIn = BooksTheme.Button("Sign in with GitHub", BooksButtonKind.Primary);
        _userCode = new TextBox
        {
            FontSize = 26,
            FontWeight = FontWeight.Bold,
            FontFamily = BooksPalette.DisplayFont,
            IsReadOnly = true,
            IsVisible = false,
            PlaceholderText = "Device code",
            Foreground = BooksPalette.AccentBrush,
            Background = BooksPalette.PanelRaisedBrush,
        };
        _copyCode = BooksTheme.Button("Copy code", BooksButtonKind.Secondary);
        _copyCode.IsVisible = false;

        _signIn.Click += async (_, _) => await RunUi(SignInAsync);
        _copyCode.Click += async (_, _) => await CopyUserCodeAsync(selectAll: true);

        _authPanel = BuildAuthPanel();
        _backButton = BooksTheme.Button("←", BooksButtonKind.Quiet);
        _backButton.HorizontalAlignment = HorizontalAlignment.Left;
        _backButton.Width = 48;
        _backButton.Click += (_, _) => Dispatcher.UIThread.Post(GoBack);

        _menuButton = BooksTheme.Button("Menu", BooksButtonKind.Secondary);
        _menuButton.HorizontalAlignment = HorizontalAlignment.Right;
        _menuButton.MinWidth = 88;
        _menuButton.ZIndex = 20;
        _menuButton.Flyout = BuildMenuFlyout();

        _listenButton = BooksTheme.Button("Listen", BooksButtonKind.Primary);
        _listenButton.Click += async (_, _) => await OnListenClickAsync();
        _undoButton = BooksTheme.Button("Undo", BooksButtonKind.Secondary);
        _undoButton.Click += (_, _) => UndoPendingChanges();
        _modeButton = BooksTheme.Button("Read", BooksButtonKind.Secondary);
        _modeButton.Click += (_, _) => ToggleChapterMode();
        _prevChapterButton = BooksTheme.Button("‹ Prev", BooksButtonKind.Quiet);
        _prevChapterButton.Click += (_, _) => GoAdjacentChapter(-1);
        _nextChapterButton = BooksTheme.Button("Next ›", BooksButtonKind.Quiet);
        _nextChapterButton.Click += (_, _) => GoAdjacentChapter(1);

        _preview = new MarkdownPreviewPane
        {
            PreviewTheme = MarkdownPreviewTheme.StudioDark,
            ZoomScale = 1.08,
            SideMarginFraction = 0.06,
            IsVisible = false,
        };

        _editorHost = new Border
        {
            Background = BooksPalette.PanelBrush,
            Focusable = true,
            IsHitTestVisible = true,
        };
        _editorHost.PointerPressed += (_, e) =>
        {
            FocusEditorInput();
            e.Handled = false;
        };

        var navRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { _prevChapterButton, _chapterMeta, _nextChapterButton },
        };
        var actionRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { _modeButton, _listenButton, _undoButton },
        };
        var toolbar = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(12, 8),
            Children = { navRow, actionRow },
        };

        var contentHost = new Grid();
        contentHost.Children.Add(_editorHost);
        contentHost.Children.Add(_preview);

        _chapterHost = new DockPanel { LastChildFill = true, IsHitTestVisible = true };
        DockPanel.SetDock(toolbar, Dock.Top);
        _chapterHost.Children.Add(toolbar);
        _chapterHost.Children.Add(contentHost);

        _chrome = BuildChrome();
        _statusShell.Margin = new Thickness(16, 8);
        _workspace = new AuthoringWorkspace
        {
            ForceMode = true,
            LayoutMode = AuthoringLayoutMode.Narrow,
            StatusBar = _statusShell,
            VisibleRegion = AuthoringRegion.Nav,
        };
        BooksTheme.ApplyRoot(_workspace);
        Content = _workspace;

        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _autoSaveTimer.Tick += (_, _) => AutoSaveTick();
        _autoSaveTimer.Start();

        _session.Changed += (_, _) => Dispatcher.UIThread.Post(OnSessionChanged);
        _speech.Changed += (_, _) => Dispatcher.UIThread.Post(OnSpeechChanged);

        AttachedToVisualTree += async (_, _) =>
        {
            await _session.TryRestoreSessionAsync();
            OnSessionChanged();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _autoSaveTimer.Stop();
            ReleaseWake();
            _speech.Stop();
        };
    }

    Control BuildAuthPanel()
    {
        var stack = new StackPanel
        {
            Spacing = 14,
            Margin = new Thickness(28, 48, 28, 28),
            Children =
            {
                BooksTheme.BrandWordmark(),
                BooksTheme.BrandTitle("Books", 34),
                BooksTheme.Muted(
                    _session.IsLocalWorkspace
                        ? $"Local bridge mode · {_session.WorkspaceRoot}"
                        : "Your manuscript library on the go — pull, edit or read a chapter, listen with Edge TTS, then save & push."),
                _signIn,
                _userCode,
                _copyCode,
            },
        };
        return new Border
        {
            Background = BooksPalette.WindowBrush,
            Child = stack,
        };
    }

    Control BuildChrome()
    {
        var row = new Grid
        {
            Background = BooksPalette.PanelBrush,
            Margin = new Thickness(0),
            MinHeight = 56,
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
        };
        var left = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            Children =
            {
                _backButton,
                BooksTheme.BrandWordmark(),
            },
        };
        _chromeTitle.VerticalAlignment = VerticalAlignment.Center;
        _chromeTitle.FontFamily = BooksPalette.DisplayFont;
        _chromeTitle.FontWeight = FontWeight.SemiBold;
        _chromeTitle.Margin = new Thickness(8, 0);
        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Children = { _menuButton },
        };
        Grid.SetColumn(left, 0);
        Grid.SetColumn(_chromeTitle, 1);
        Grid.SetColumn(right, 2);
        row.Children.Add(left);
        row.Children.Add(_chromeTitle);
        row.Children.Add(right);
        return row;
    }

    MenuFlyout BuildMenuFlyout()
    {
        var pull = new MenuItem { Header = "Pull from GitHub" };
        var push = new MenuItem { Header = "Save / Commit / Push" };
        var undo = new MenuItem { Header = "Undo pending changes" };
        var read = new MenuItem { Header = "Toggle reading mode" };
        var library = new MenuItem { Header = "Library" };
        var signOut = new MenuItem { Header = "Sign out" };
        pull.Click += async (_, _) => await RunUi(PullAsync);
        push.Click += async (_, _) => await RunUi(PushAsync);
        undo.Click += (_, _) => UndoPendingChanges();
        read.Click += (_, _) =>
        {
            if (_screen == Screen.Chapter)
                ToggleChapterMode();
        };
        library.Click += (_, _) => ShowLibrary();
        signOut.Click += async (_, _) => await RunUi(SignOutAsync);
        return new MenuFlyout
        {
            Items = { pull, push, undo, read, new Separator(), library, signOut },
        };
    }

    void OnSpeechChanged()
    {
        if (_speech.IsSpeaking)
            EnsureWake();
        else
            ReleaseWake();
        RefreshChrome();
        UpdateChapterActions();
    }

    void EnsureWake()
    {
        _wake ??= _wakeLock.Acquire("booksmobile-listen");
    }

    void ReleaseWake()
    {
        _wake?.Dispose();
        _wake = null;
    }

    void OnSessionChanged()
    {
        if (!_session.IsSignedIn)
        {
            _screen = Screen.Auth;
            RenderCurrentScreen();
            UpdateAuthGadgets();
            SetStatus(_session.Status);
            return;
        }

        UpdateAuthGadgets();
        if (_screen is Screen.Auth)
            ShowLibrary();
        else
            RenderCurrentScreen();
        SetStatus(_session.Status);
    }

    void UpdateAuthGadgets()
    {
        var code = _session.UserCode;
        var hasCode = !string.IsNullOrWhiteSpace(code);
        _userCode.Text = code ?? string.Empty;
        _userCode.IsVisible = hasCode;
        _copyCode.IsVisible = hasCode;
        if (!hasCode)
        {
            _copyCode.Content = "Copy code";
            _lastClipboardCode = null;
        }
        else if (!string.Equals(code, _lastClipboardCode, StringComparison.Ordinal))
            _ = CopyUserCodeAsync(selectAll: true);
    }

    async Task CopyUserCodeAsync(bool selectAll)
    {
        var code = _session.UserCode;
        if (string.IsNullOrWhiteSpace(code))
            return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
            return;
        await clipboard.SetTextAsync(code).ConfigureAwait(true);
        _lastClipboardCode = code;
        if (selectAll)
        {
            _userCode.Focus();
            _userCode.SelectAll();
        }

        _copyCode.Content = "Copied";
        SetStatus($"Code {code} copied — paste it on GitHub.");
    }

    void ShowLibrary()
    {
        FlushEditorToDisk();
        _speech.Stop();
        ReleaseWake();
        _openBook = null;
        _openSeriesTitle = null;
        _openChapter = null;
        _loadedPath = null;
        _baselineText = null;
        _chapterTitle = null;
        _chapterMode = ChapterMode.Edit;
        _screen = Screen.Library;
        RenderCurrentScreen();
    }

    void ShowBook(BookInfo book, string? seriesTitle)
    {
        FlushEditorToDisk();
        _speech.Stop();
        ReleaseWake();
        _openBook = book;
        _openSeriesTitle = seriesTitle;
        _openChapter = null;
        _loadedPath = null;
        _baselineText = null;
        _chapterTitle = null;
        _chapterMode = ChapterMode.Edit;
        _screen = Screen.Book;
        RenderCurrentScreen();
    }

    void OpenChapter(ChapterInfo chapter)
    {
        if (_openBook is null)
            return;

        try
        {
            FlushEditorToDisk();
            _speech.Stop();
            ReleaseWake();

            if (!File.Exists(chapter.FilePath))
            {
                SetStatus($"Missing file: {chapter.FilePath}");
                return;
            }

            var text = File.ReadAllText(chapter.FilePath);
            _session.OpenChapter(chapter.FilePath);
            _loadedPath = chapter.FilePath;
            _baselineText = text;
            _chapterTitle = chapter.Title;
            _openChapter = chapter;
            _screen = Screen.Chapter;
            ReplaceEditor(text);
            ApplyChapterMode();
            RenderCurrentScreen();
            SetStatus($"{(_chapterMode == ChapterMode.Read ? "Reading" : "Editing")} · {chapter.Title}");
            if (_chapterMode == ChapterMode.Edit)
                Dispatcher.UIThread.Post(FocusEditorInput, DispatcherPriority.Input);
        }
        catch (Exception ex)
        {
            SetStatus($"Could not open chapter: {ex.Message}");
        }
    }

    void GoAdjacentChapter(int delta)
    {
        if (_openBook is null || _openChapter is null)
            return;
        var list = _openBook.Chapters;
        var idx = -1;
        for (var i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i].FilePath, _openChapter.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                idx = i;
                break;
            }
        }

        var next = idx + delta;
        if (idx < 0 || next < 0 || next >= list.Count)
        {
            SetStatus(delta < 0 ? "Already at first chapter." : "Already at last chapter.");
            return;
        }

        OpenChapter(list[next]);
    }

    void ToggleChapterMode()
    {
        if (_screen != Screen.Chapter)
            return;
        _chapterMode = _chapterMode == ChapterMode.Read ? ChapterMode.Edit : ChapterMode.Read;
        ApplyChapterMode();
        RefreshChrome();
        SetStatus(_chapterMode == ChapterMode.Read
            ? $"Reading · {_chapterTitle}"
            : $"Editing · {_chapterTitle}");
        if (_chapterMode == ChapterMode.Edit)
            Dispatcher.UIThread.Post(FocusEditorInput, DispatcherPriority.Input);
    }

    void ApplyChapterMode()
    {
        var reading = _chapterMode == ChapterMode.Read;
        _preview.IsVisible = reading;
        _editorHost.IsVisible = !reading;
        _modeButton.Content = reading ? "Edit" : "Read";
        BooksTheme.StyleButton(_modeButton, reading ? BooksButtonKind.Primary : BooksButtonKind.Secondary);
        if (reading)
            SyncPreviewFromEditor();
        UpdateChapterActions();
    }

    void SyncPreviewFromEditor()
    {
        _preview.Markdown = _editor?.Text ?? string.Empty;
    }

    void ReplaceEditor(string text)
    {
        if (_editor is not null)
            _editor.PropertyChanged -= OnEditorPropertyChanged;

        _editor = new MarkdownSourceEditor
        {
            HighlightingProfile = MarkdownSourceHighlightingProfile.BookAuthoring,
            WordWrap = true,
            BaseFontSize = 17,
            PlaceholderText = "Tap here to edit this chapter.",
            IsHitTestVisible = true,
            Focusable = true,
        };
        _editor.PropertyChanged += OnEditorPropertyChanged;
        _suppressDirty = true;
        _editor.Text = text;
        _suppressDirty = false;
        _editorHost.Child = _editor;
        SyncPreviewFromEditor();
    }

    void OnEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MarkdownSourceEditor.TextProperty && !_suppressDirty)
        {
            _session.NoteCurrentDirty();
            if (_chapterMode == ChapterMode.Read)
                SyncPreviewFromEditor();
            UpdateChapterActions();
            RefreshChrome();
        }
    }

    void FocusEditorInput()
    {
        if (_editor is null || _screen != Screen.Chapter || _chapterMode != ChapterMode.Edit)
            return;
        _editor.IsEnabled = true;
        _editor.IsHitTestVisible = true;
        _editor.Focusable = true;
        _editor.FocusEditor();
    }

    bool HasPendingChanges()
    {
        if (_editor is null || _baselineText is null)
            return false;
        return !string.Equals(_editor.Text ?? string.Empty, _baselineText, StringComparison.Ordinal);
    }

    void UndoPendingChanges()
    {
        if (_screen != Screen.Chapter || _editor is null || _baselineText is null)
        {
            SetStatus("Nothing to undo.");
            return;
        }

        if (!HasPendingChanges())
        {
            SetStatus("No pending changes.");
            return;
        }

        _speech.Stop();
        _suppressDirty = true;
        _editor.Text = _baselineText;
        _suppressDirty = false;
        if (!string.IsNullOrWhiteSpace(_loadedPath))
            File.WriteAllText(_loadedPath, _baselineText);
        SyncPreviewFromEditor();
        UpdateChapterActions();
        RefreshChrome();
        if (_chapterMode == ChapterMode.Edit)
            FocusEditorInput();
        SetStatus("Pending changes discarded.");
    }

    void AutoSaveTick()
    {
        if (_screen != Screen.Chapter || !HasPendingChanges())
            return;
        try
        {
            FlushEditorToDisk();
            SetStatus($"Auto-saved · {_chapterTitle}");
        }
        catch (Exception ex)
        {
            SetStatus($"Auto-save failed: {ex.Message}");
        }
    }

    void GoBack()
    {
        _speech.Stop();
        ReleaseWake();
        SetBusy(false);

        switch (_screen)
        {
            case Screen.Chapter:
                FlushEditorToDisk();
                _loadedPath = null;
                _baselineText = null;
                _chapterTitle = null;
                _openChapter = null;
                _chapterMode = ChapterMode.Edit;
                _screen = _openBook is null ? Screen.Library : Screen.Book;
                RenderCurrentScreen();
                SetStatus(_openBook is null ? "Library" : _openBook.Title);
                break;
            case Screen.Book:
                ShowLibrary();
                break;
            default:
                ShowLibrary();
                break;
        }
    }

    void RenderCurrentScreen()
    {
        RefreshChrome();
        switch (_screen)
        {
            case Screen.Auth:
                _workspace.TopBar = null;
                _workspace.Nav = _authPanel;
                _workspace.ShowRegion(AuthoringRegion.Nav);
                break;
            case Screen.Library:
                _workspace.TopBar = _chrome;
                _workspace.Nav = BuildLibraryPage();
                _workspace.ShowRegion(AuthoringRegion.Nav);
                break;
            case Screen.Book:
                _workspace.TopBar = _chrome;
                _workspace.Context = BuildBookPage();
                _workspace.ShowRegion(AuthoringRegion.Context);
                break;
            case Screen.Chapter:
                _workspace.TopBar = _chrome;
                _workspace.Primary = _chapterHost;
                _workspace.ShowRegion(AuthoringRegion.Primary);
                break;
            default:
                _workspace.TopBar = _chrome;
                _workspace.Nav = new TextBlock { Text = string.Empty };
                _workspace.ShowRegion(AuthoringRegion.Nav);
                break;
        }

        if (_screen == Screen.Chapter && _chapterMode == ChapterMode.Edit)
            Dispatcher.UIThread.Post(FocusEditorInput, DispatcherPriority.Input);
    }

    void RefreshChrome()
    {
        _backButton.IsVisible = _screen is Screen.Book or Screen.Chapter;
        _backButton.IsEnabled = true;
        _menuButton.IsVisible = true;
        _menuButton.IsEnabled = true;
        var dirty = HasPendingChanges() ? " · edited" : string.Empty;
        var mode = _screen == Screen.Chapter
            ? (_chapterMode == ChapterMode.Read ? " · read" : " · edit")
            : string.Empty;
        _chromeTitle.Text = _screen switch
        {
            Screen.Library => "Library",
            Screen.Book => _openBook?.Title ?? "Book",
            Screen.Chapter => $"{_chapterTitle ?? "Chapter"}{mode}{dirty}",
            _ => "Books",
        };
        UpdateChapterActions();
    }

    Control BuildLibraryPage()
    {
        var series = _session.LoadSeries();
        var standalone = _session.LoadStandaloneBooks();
        var list = new StackPanel { Spacing = 4, Margin = new Thickness(16) };

        if (series.Count == 0 && standalone.Count == 0)
        {
            list.Children.Add(BooksTheme.BrandTitle("Your books", 24));
            list.Children.Add(BooksTheme.Muted(
                "Nothing synced yet. Use Menu → Pull from GitHub to download your library."));
            var pull = BooksTheme.Button("Pull from GitHub", BooksButtonKind.Primary);
            pull.Click += async (_, _) => await RunUi(PullAsync);
            list.Children.Add(pull);
            return new ScrollViewer { Content = list };
        }

        list.Children.Add(BooksTheme.BrandTitle("Your books", 24));
        list.Children.Add(BooksTheme.Muted("Pick a book — read, edit, or listen on the go."));

        foreach (var s in series)
        {
            list.Children.Add(BooksTheme.Muted($"Series · {s.Title}", 12));
            foreach (var book in s.Books)
                list.Children.Add(MakeBookCard(book, s.Title));
        }

        if (standalone.Count > 0)
        {
            list.Children.Add(BooksTheme.Muted("Collections", 12));
            foreach (var book in standalone)
                list.Children.Add(MakeBookCard(book, null));
        }

        return new ScrollViewer { Content = list, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    }

    Control MakeBookCard(BookInfo book, string? seriesTitle)
    {
        var title = BooksTheme.Body(book.Title, 17);
        title.FontFamily = BooksPalette.DisplayFont;
        title.FontWeight = FontWeight.SemiBold;
        var meta = BooksTheme.Muted(
            $"{book.Chapters.Count} chapter{(book.Chapters.Count == 1 ? "" : "s")}"
            + (string.IsNullOrWhiteSpace(book.Author) ? "" : $" · {book.Author}"));
        var stack = new StackPanel { Spacing = 4, Children = { title, meta } };
        if (!string.IsNullOrWhiteSpace(book.Subtitle))
            stack.Children.Insert(1, BooksTheme.Muted(book.Subtitle!, 13));

        var btn = new Button
        {
            Content = stack,
            Background = BooksPalette.PanelBrush,
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 47, 223, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16, 14),
            Margin = new Thickness(0, 0, 0, 10),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        btn.Click += (_, _) => Dispatcher.UIThread.Post(() => ShowBook(book, seriesTitle));
        return btn;
    }

    Control BuildBookPage()
    {
        if (_openBook is null)
            return BooksTheme.Muted("No book selected.");

        var list = new StackPanel { Spacing = 8, Margin = new Thickness(16) };
        list.Children.Add(BooksTheme.BrandTitle(_openBook.Title, 26));
        if (!string.IsNullOrWhiteSpace(_openSeriesTitle))
            list.Children.Add(BooksTheme.Muted(_openSeriesTitle!));
        if (!string.IsNullOrWhiteSpace(_openBook.Subtitle))
            list.Children.Add(BooksTheme.Muted(_openBook.Subtitle!));
        list.Children.Add(BooksTheme.Muted("Chapters — opens in reading mode; tap Edit to change text.", 13));

        foreach (var ch in _openBook.Chapters)
        {
            var label = BooksTheme.Body(ch.Title, 16);
            var kind = BooksTheme.Muted(ch.Kind.ToString(), 11);
            var row = new StackPanel { Spacing = 2, Children = { label, kind } };
            var btn = new Button
            {
                Content = row,
                Background = BooksPalette.PanelBrush,
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 47, 223, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(16, 14),
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            btn.Click += (_, _) => Dispatcher.UIThread.Post(() =>
            {
                _chapterMode = ChapterMode.Read;
                OpenChapter(ch);
            });
            list.Children.Add(btn);
        }

        return new ScrollViewer { Content = list, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    }

    void UpdateChapterActions()
    {
        if (_speech.IsSpeaking)
            _listenButton.Content = "Stop";
        else
        {
            var text = _editor?.Text ?? string.Empty;
            _listenButton.Content = _speech.HasCachedAudio(text) ? "Listen ✓" : "Listen";
        }

        BooksTheme.StyleButton(_listenButton, BooksButtonKind.Primary);
        var pending = HasPendingChanges();
        _undoButton.IsEnabled = pending && !_speech.IsSpeaking;
        _undoButton.Opacity = pending ? 1 : 0.45;
        _listenButton.IsEnabled = true;

        var idx = CurrentChapterIndex();
        var count = _openBook?.Chapters.Count ?? 0;
        _chapterMeta.Text = idx >= 0 && count > 0 ? $"{idx + 1} / {count}" : string.Empty;
        _prevChapterButton.IsEnabled = idx > 0;
        _nextChapterButton.IsEnabled = idx >= 0 && idx < count - 1;
        _prevChapterButton.Opacity = _prevChapterButton.IsEnabled ? 1 : 0.4;
        _nextChapterButton.Opacity = _nextChapterButton.IsEnabled ? 1 : 0.4;
    }

    int CurrentChapterIndex()
    {
        if (_openBook is null || _openChapter is null)
            return -1;
        for (var i = 0; i < _openBook.Chapters.Count; i++)
        {
            if (string.Equals(_openBook.Chapters[i].FilePath, _openChapter.FilePath, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    async Task OnListenClickAsync()
    {
        if (_speech.IsSpeaking)
        {
            _speech.Stop();
            ReleaseWake();
            SetStatus("Stopped.");
            UpdateChapterActions();
            if (_chapterMode == ChapterMode.Edit)
                Dispatcher.UIThread.Post(FocusEditorInput, DispatcherPriority.Input);
            return;
        }

        try
        {
            var text = _editor?.Text ?? string.Empty;
            var cached = _speech.HasCachedAudio(text);
            SetStatus(cached
                ? "Playing cached audio (Ava)…"
                : "Synthesizing with Edge TTS (Ava)…");
            EnsureWake();
            UpdateChapterActions();
            await _speech.SpeakChapterAsync(text);
            SetStatus(cached ? "Finished (from cache)." : "Finished chapter.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Stopped.");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
        finally
        {
            ReleaseWake();
            UpdateChapterActions();
            RefreshChrome();
            if (_chapterMode == ChapterMode.Edit)
                Dispatcher.UIThread.Post(FocusEditorInput, DispatcherPriority.Input);
        }
    }

    void SetStatus(string? text)
    {
        _statusShell.Text = text ?? string.Empty;
    }

    async Task SignInAsync()
    {
        await _session.SignInAsync();
        OnSessionChanged();
    }

    async Task SignOutAsync()
    {
        FlushEditorToDisk();
        _speech.Stop();
        ReleaseWake();
        await _session.SignOutAsync();
        _openBook = null;
        _openChapter = null;
        _loadedPath = null;
        _baselineText = null;
        OnSessionChanged();
    }

    async Task PullAsync()
    {
        FlushEditorToDisk();
        await _session.PullAsync();
        if (_screen == Screen.Library || _openBook is null)
            ShowLibrary();
        else if (_screen == Screen.Book && _openBook is not null)
        {
            var refreshed = FindBook(_openBook.Id, _openBook.SeriesId);
            if (refreshed is not null)
                ShowBook(refreshed, _openSeriesTitle);
            else
                ShowLibrary();
        }
        else if (_screen == Screen.Chapter && !string.IsNullOrWhiteSpace(_loadedPath) && File.Exists(_loadedPath))
        {
            var text = File.ReadAllText(_loadedPath);
            _baselineText = text;
            ReplaceEditor(text);
            ApplyChapterMode();
            RenderCurrentScreen();
            SetStatus("Pulled — chapter reloaded.");
        }
        else
            RenderCurrentScreen();
    }

    async Task PushAsync()
    {
        FlushEditorToDisk();
        if (!string.IsNullOrWhiteSpace(_loadedPath) && _editor is not null)
            _baselineText = _editor.Text ?? string.Empty;
        await _session.SaveCommitPushAsync();
        UpdateChapterActions();
        RefreshChrome();
        SetStatus(_session.Status);
    }

    BookInfo? FindBook(string bookId, string? seriesId)
    {
        foreach (var s in _session.LoadSeries())
        {
            if (seriesId is not null && !string.Equals(s.Id, seriesId, StringComparison.OrdinalIgnoreCase))
                continue;
            var hit = s.Books.FirstOrDefault(b => string.Equals(b.Id, bookId, StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
                return hit;
        }

        return _session.LoadStandaloneBooks()
            .FirstOrDefault(b => string.Equals(b.Id, bookId, StringComparison.OrdinalIgnoreCase));
    }

    void FlushEditorToDisk()
    {
        if (string.IsNullOrWhiteSpace(_loadedPath) || _editor is null)
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(_loadedPath)!);
        File.WriteAllText(_loadedPath, _editor.Text ?? string.Empty);
        _session.NoteCurrentDirty();
    }

    async Task RunUi(Func<Task> action)
    {
        try
        {
            SetBusy(true);
            await action();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    void SetBusy(bool busy)
    {
        _busy = busy;
        _signIn.IsEnabled = !busy;
        _menuButton.IsEnabled = true;
        _menuButton.IsVisible = true;
        _backButton.IsEnabled = true;
        _listenButton.IsEnabled = true;
        UpdateChapterActions();
    }
}
