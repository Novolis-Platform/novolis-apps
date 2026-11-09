using Novolis.Avalonia.GraphicalProfile;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using BooksMobile.Services;
using Novolis.Avalonia.Mobile;
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
        ReadAnything,
        PasteSelection,
    }

    enum ChapterMode
    {
        Edit,
        Read,
    }

    readonly BooksMobileSession _session;
    readonly ChapterSpeechService _speech;
    readonly ReviewSelectionImporter _importer;
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
    readonly StackPanel _chapterTopBar;
    readonly Border _chapterBottomBar;
    readonly Button _listenButton;
    readonly Button _undoButton;
    readonly Button _saveButton;
    readonly Button _modeButton;
    readonly Button _prevChapterButton;
    readonly Button _nextChapterButton;
    readonly TextBlock _chapterMeta = BooksTheme.Muted(string.Empty, 12);
    readonly Border _editorHost;
    readonly MarkdownPreviewPane _preview;
    readonly ReadAnythingView _readAnything = new();
    readonly PasteSelectionView _pasteSelection = new();
    readonly DispatcherTimer _draftHintTimer;

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
    string? _speechMarkdownSource;
    string? _speechMarkdownCache;
    bool _suppressDirty;
    bool _busy;

    public MainView(
        BooksMobileSession session,
        ChapterSpeechService speech,
        ReviewSelectionImporter importer,
        IScreenWakeLock wakeLock)
    {
        _session = session;
        _speech = speech;
        _importer = importer;
        _wakeLock = wakeLock;
        Background = GraphicalProfile.BackgroundBrush;
        Focusable = true;

        _signIn = BooksTheme.Button("Sign in with GitHub", BooksButtonKind.Primary);
        _userCode = new TextBox
        {
            FontSize = 26,
            FontWeight = FontWeight.Bold,
            FontFamily = GraphicalProfile.BodyFont,
            IsReadOnly = true,
            IsVisible = false,
            PlaceholderText = "Device code",
            Foreground = GraphicalProfile.AccentBrush,
            Background = GraphicalProfile.RaisedBrush,
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

        _listenButton = BooksTheme.Button("Listen", BooksButtonKind.Secondary);
        _listenButton.Click += async (_, _) => await OnListenClickAsync();
        _undoButton = BooksTheme.Button("Discard", BooksButtonKind.Quiet);
        _undoButton.Click += (_, _) => UndoPendingChanges();
        _saveButton = BooksTheme.Button("Save", BooksButtonKind.Secondary);
        _saveButton.Click += (_, _) => SaveDraftToWorkspace();
        _modeButton = BooksTheme.Button("Edit", BooksButtonKind.Primary);
        _modeButton.Click += (_, _) => ToggleChapterMode();
        _prevChapterButton = BooksTheme.Button("‹ Prev", BooksButtonKind.Quiet);
        _prevChapterButton.Click += (_, _) => GoAdjacentChapter(-1);
        _nextChapterButton = BooksTheme.Button("Next ›", BooksButtonKind.Quiet);
        _nextChapterButton.Click += (_, _) => GoAdjacentChapter(1);

        _preview = new MarkdownPreviewPane
        {
            PreviewTheme = MarkdownPreviewTheme.StudioDark,
            ZoomScale = 1.18,
            SideMarginFraction = 0.04,
            IsVisible = false,
        };

        _editorHost = new Border
        {
            Background = GraphicalProfile.SurfaceBrush,
            Focusable = true,
            IsHitTestVisible = true,
        };
        _editorHost.PointerPressed += (_, e) =>
        {
            FocusEditorInput();
            e.Handled = false;
        };

        _chapterTopBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(12, 8, 12, 4),
            Children = { _prevChapterButton, _chapterMeta, _nextChapterButton },
        };
        var actionRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children = { _modeButton, _listenButton, _saveButton, _undoButton },
        };
        _chapterBottomBar = new Border
        {
            Background = GraphicalProfile.SurfaceBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(12, 10),
            Child = actionRow,
        };

        var contentHost = new Grid();
        contentHost.Children.Add(_editorHost);
        contentHost.Children.Add(_preview);

        _chapterHost = new DockPanel { LastChildFill = true, IsHitTestVisible = true };
        DockPanel.SetDock(_chapterTopBar, Dock.Top);
        DockPanel.SetDock(_chapterBottomBar, Dock.Bottom);
        _chapterHost.Children.Add(_chapterTopBar);
        _chapterHost.Children.Add(_chapterBottomBar);
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

        // Draft reminder only — never writes manuscript files until Save / Push.
        _draftHintTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        _draftHintTimer.Tick += (_, _) => DraftHintTick();
        _draftHintTimer.Start();

        _session.Changed += (_, _) => Dispatcher.UIThread.Post(OnSessionChanged);
        _speech.Changed += (_, _) => Dispatcher.UIThread.Post(OnSpeechChanged);
        _speech.PlaybackStarted += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (_speech.IsSpeaking)
                SetStatus("Speaking…");
        });
        _readAnything.ListenRequested += async (_, _) => await OnReadAnythingListenAsync();
        _pasteSelection.ImportRequested += async (_, _) => await RunUi(ImportSelectionAsync);

        AttachedToVisualTree += async (_, _) =>
        {
            await _session.TryRestoreSessionAsync();
            OnSessionChanged();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _draftHintTimer.Stop();
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
                        ? "Local workspace — read, listen, and edit. Save writes files; leave without Save and drafts disappear."
                        : "Your library on the go — pull, read or listen, edit a draft, then save & push."),
                _signIn,
                _userCode,
                _copyCode,
            },
        };
        return new Border
        {
            Background = GraphicalProfile.BackgroundBrush,
            Child = new ScrollViewer
            {
                Content = stack,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            },
        };
    }

    Control BuildChrome()
    {
        var row = new Grid
        {
            Background = GraphicalProfile.SurfaceBrush,
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
        _chromeTitle.FontFamily = GraphicalProfile.BodyFont;
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
        var push = new MenuItem { Header = "Commit / Push" };
        var undo = new MenuItem { Header = "Discard draft" };
        var read = new MenuItem { Header = "Toggle edit / read" };
        var readAnything = new MenuItem { Header = "Read anything" };
        var pasteSelection = new MenuItem { Header = "Paste selection…" };
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
        readAnything.Click += (_, _) => ShowReadAnything();
        pasteSelection.Click += (_, _) =>
        {
            if (!_session.IsReviewMode)
            {
                SetStatus("Switch to Review mode to paste a selection.");
                return;
            }

            ShowPasteSelection();
        };
        library.Click += (_, _) => ShowLibrary();
        signOut.Click += async (_, _) => await RunUi(SignOutAsync);
        return new MenuFlyout
        {
            Items =
            {
                pull, push, undo, read, readAnything, pasteSelection,
                new Separator(), library, signOut,
            },
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
        if (_screen == Screen.ReadAnything)
            RefreshReadAnythingListen();
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
        AbandonDraft();
        _speech.Stop();
        ReleaseWake();
        _openBook = null;
        _openSeriesTitle = null;
        _openChapter = null;
        _loadedPath = null;
        _baselineText = null;
        _chapterTitle = null;
        _chapterMode = ChapterMode.Read;
        _screen = Screen.Library;
        RenderCurrentScreen();
    }

    void ShowReadAnything()
    {
        AbandonDraft();
        _speech.Stop();
        ReleaseWake();
        _screen = Screen.ReadAnything;
        RenderCurrentScreen();
        SetStatus("Scratch reader — not part of your library.");
        RefreshReadAnythingListen();
    }

    void ShowPasteSelection()
    {
        AbandonDraft();
        _speech.Stop();
        ReleaseWake();
        _screen = Screen.PasteSelection;
        RenderCurrentScreen();
        _pasteSelection.SetStatus("Paste a template-valid selection, then Import + audio + push.");
    }

    void ShowBook(BookInfo book, string? seriesTitle)
    {
        AbandonDraft();
        _speech.Stop();
        ReleaseWake();
        _openBook = book;
        _openSeriesTitle = seriesTitle;
        _openChapter = null;
        _loadedPath = null;
        _baselineText = null;
        _chapterTitle = null;
        _chapterMode = ChapterMode.Read;
        _screen = Screen.Book;
        RenderCurrentScreen();
    }

    void OpenChapter(ChapterInfo chapter)
    {
        if (_openBook is null)
            return;

        try
        {
            AbandonDraft();
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
            _speechMarkdownSource = null;
            _speechMarkdownCache = null;
            _chapterMode = ChapterMode.Read;
            _screen = Screen.Chapter;
            ReplaceEditor(text);
            ApplyChapterMode();
            RenderCurrentScreen();
            var unit = _session.IsReviewMode ? "selection" : "chapter";
            SetStatus($"Reading {unit} · {chapter.Title}");
        }
        catch (Exception ex)
        {
            SetStatus($"Could not open: {ex.Message}");
        }
    }

    string PrepareSpeechMarkdown(string markdown)
    {
        if (!_session.IsReviewMode)
            return markdown;
        if (_speechMarkdownSource == markdown && _speechMarkdownCache is not null)
            return _speechMarkdownCache;
        try
        {
            _speechMarkdownCache = ReviewSpeechText.ToSpeechMarkdown(markdown);
            _speechMarkdownSource = markdown;
            return _speechMarkdownCache;
        }
        catch (Exception ex)
        {
            SetStatus($"Speech prep failed: {ex.Message}");
            return markdown;
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
        // In read mode, Edit is the primary escape; in edit mode, Read returns to the reader.
        BooksTheme.StyleButton(_modeButton, BooksButtonKind.Primary);
        _preview.ZoomScale = reading ? 1.18 : 1.08;
        _chapterTopBar.Opacity = reading ? 0.92 : 1;
        if (reading)
            SyncPreviewFromEditor();
        UpdateChapterActions();
    }

    void SyncPreviewFromEditor()
    {
        try
        {
            var text = _editor?.Text ?? string.Empty;
            // Review masthead HTML / !!! admonitions have crashed HtmlPanel on Android.
            if (_session.IsReviewMode)
                text = ReviewSpeechText.ToPreviewMarkdown(text);
            _preview.Markdown = text;
        }
        catch (Exception ex)
        {
            _preview.Markdown = $"*(Preview failed: {ex.Message})*";
            SetStatus($"Preview failed: {ex.Message}");
        }
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
        SyncPreviewFromEditor();
        UpdateChapterActions();
        RefreshChrome();
        if (_chapterMode == ChapterMode.Edit)
            FocusEditorInput();
        SetStatus("Draft discarded — files unchanged.");
    }

    void DraftHintTick()
    {
        if (_screen != Screen.Chapter || !HasPendingChanges() || _speech.IsSpeaking)
            return;
        SetStatus($"Unsaved draft · {_chapterTitle} — Save writes files; leave without Save and it disappears.");
    }

    void SaveDraftToWorkspace()
    {
        if (_screen != Screen.Chapter || _editor is null || string.IsNullOrWhiteSpace(_loadedPath))
        {
            SetStatus("Nothing to save.");
            return;
        }

        if (!HasPendingChanges())
        {
            SetStatus("Already saved.");
            return;
        }

        try
        {
            FlushEditorToDisk();
            _baselineText = _editor.Text ?? string.Empty;
            UpdateChapterActions();
            RefreshChrome();
            SetStatus($"Saved · {_chapterTitle}");
        }
        catch (Exception ex)
        {
            SetStatus($"Save failed: {ex.Message}");
        }
    }

    /// <summary>Drops in-memory edits without writing manuscript files.</summary>
    void AbandonDraft()
    {
        if (_editor is null || _baselineText is null)
            return;
        if (!HasPendingChanges())
            return;
        _suppressDirty = true;
        _editor.Text = _baselineText;
        _suppressDirty = false;
        SyncPreviewFromEditor();
    }

    void GoBack()
    {
        _speech.Stop();
        ReleaseWake();
        SetBusy(false);

        switch (_screen)
        {
            case Screen.Chapter:
                AbandonDraft();
                _loadedPath = null;
                _baselineText = null;
                _chapterTitle = null;
                _openChapter = null;
                _chapterMode = ChapterMode.Read;
                _screen = _openBook is null ? Screen.Library : Screen.Book;
                RenderCurrentScreen();
                SetStatus(_openBook is null ? "Library" : _openBook.Title);
                break;
            case Screen.Book:
            case Screen.ReadAnything:
            case Screen.PasteSelection:
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
            case Screen.ReadAnything:
                _workspace.TopBar = _chrome;
                _workspace.Primary = _readAnything;
                _workspace.ShowRegion(AuthoringRegion.Primary);
                RefreshReadAnythingListen();
                break;
            case Screen.PasteSelection:
                _workspace.TopBar = _chrome;
                _workspace.Primary = _pasteSelection;
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
        _backButton.IsVisible = _screen is Screen.Book or Screen.Chapter or Screen.ReadAnything
            or Screen.PasteSelection;
        _backButton.IsEnabled = true;
        _menuButton.IsVisible = true;
        _menuButton.IsEnabled = true;
        var dirty = HasPendingChanges() ? " · draft" : string.Empty;
        var mode = _screen == Screen.Chapter
            ? (_chapterMode == ChapterMode.Read ? " · read" : " · edit")
            : string.Empty;
        var unit = _session.IsReviewMode ? "Selection" : "Chapter";
        _chromeTitle.Text = _screen switch
        {
            Screen.Library => _session.IsReviewMode ? "Review" : "Your books",
            Screen.Book => _openBook?.Title ?? (_session.IsReviewMode ? "Archive" : "Book"),
            Screen.Chapter => $"{_chapterTitle ?? unit}{mode}{dirty}",
            Screen.ReadAnything => "Read anything",
            Screen.PasteSelection => "Paste selection",
            _ => "Books",
        };
        UpdateChapterActions();
        if (_screen == Screen.ReadAnything)
            RefreshReadAnythingListen();
    }

    Control BuildLibraryPage()
    {
        var series = _session.LoadSeries();
        var standalone = _session.LoadStandaloneBooks();
        var list = new StackPanel { Spacing = 4, Margin = new Thickness(16) };

        list.Children.Add(BuildWorkspaceSwitcher());

        if (_session.IsReviewMode)
        {
            list.Children.Add(BooksTheme.BrandTitle("Review", 22));
            list.Children.Add(BooksTheme.Muted("Selections from the Review archive."));
        }
        else
        {
            list.Children.Add(BooksTheme.BrandTitle("Your books", 22));
            list.Children.Add(BooksTheme.Muted("Open a book to read. Edit is one tap away."));
        }

        if (series.Count == 0 && standalone.Count == 0)
        {
            list.Children.Add(BooksTheme.Muted(
                "Nothing synced yet. Use Menu → Pull from GitHub to download."));
            var pull = BooksTheme.Button("Pull from GitHub", BooksButtonKind.Primary);
            pull.Click += async (_, _) => await RunUi(PullAsync);
            list.Children.Add(pull);
            return new ScrollViewer { Content = list };
        }

        foreach (var s in series)
        {
            list.Children.Add(BooksTheme.Muted($"Series · {s.Title}", 12));
            foreach (var book in s.Books)
                list.Children.Add(MakeBookCard(book, s.Title));
        }

        if (standalone.Count > 0)
        {
            list.Children.Add(BooksTheme.Muted(
                _session.IsReviewMode ? "Archive collections" : "Standalone books / collections",
                12));
            foreach (var book in standalone)
                list.Children.Add(MakeBookCard(book, null));
        }

        return new ScrollViewer
        {
            Content = list,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
    }

    Control BuildWorkspaceSwitcher()
    {
        var booksBtn = BooksTheme.Button("Books", BooksButtonKind.Secondary);
        var reviewBtn = BooksTheme.Button("Review", BooksButtonKind.Secondary);
        if (_session.Mode == WorkspaceMode.Books)
            BooksTheme.StyleButton(booksBtn, BooksButtonKind.Primary);
        else
            BooksTheme.StyleButton(reviewBtn, BooksButtonKind.Primary);

        booksBtn.HorizontalAlignment = HorizontalAlignment.Left;
        reviewBtn.HorizontalAlignment = HorizontalAlignment.Left;
        booksBtn.MinWidth = 0;
        reviewBtn.MinWidth = 0;
        booksBtn.Click += async (_, _) => await RunUi(async () =>
        {
            await _session.SwitchModeAsync(WorkspaceMode.Books);
            ShowLibrary();
        });
        reviewBtn.Click += async (_, _) => await RunUi(async () =>
        {
            await _session.SwitchModeAsync(WorkspaceMode.Review);
            ShowLibrary();
        });

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 12),
            Children = { booksBtn, reviewBtn },
        };
    }

    Control MakeBookCard(BookInfo book, string? seriesTitle)
    {
        var title = BooksTheme.Body(book.Title, 17);
        title.FontFamily = GraphicalProfile.BodyFont;
        title.FontWeight = FontWeight.SemiBold;
        var unit = _session.IsReviewMode ? "selection" : "chapter";
        var meta = BooksTheme.Muted(
            $"{book.Chapters.Count} {unit}{(book.Chapters.Count == 1 ? "" : "s")}"
            + (string.IsNullOrWhiteSpace(book.Author) ? "" : $" · {book.Author}"));
        var stack = new StackPanel { Spacing = 4, Children = { title, meta } };
        if (!string.IsNullOrWhiteSpace(book.Subtitle))
            stack.Children.Insert(1, BooksTheme.Muted(book.Subtitle!, 13));

        var btn = new Button
        {
            Content = stack,
            Background = GraphicalProfile.SurfaceBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(GraphicalProfileColors.CardRadius),
            Padding = new Thickness(GraphicalProfileColors.CardPadding),
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
        list.Children.Add(BooksTheme.Muted(
            _session.IsReviewMode
                ? "Tap a selection to read. Use Edit when you want to change text."
                : "Tap a chapter to read. Use Edit when you want to change text.",
            13));

        foreach (var ch in _openBook.Chapters)
        {
            var label = BooksTheme.Body(ch.Title, 16);
            var kind = BooksTheme.Muted(_session.IsReviewMode ? "Selection" : ch.Kind.ToString(), 11);
            var row = new StackPanel { Spacing = 2, Children = { label, kind } };
            var btn = new Button
            {
                Content = row,
                Background = GraphicalProfile.SurfaceBrush,
                BorderBrush = GraphicalProfile.BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(GraphicalProfileColors.CardRadius),
                Padding = new Thickness(GraphicalProfileColors.CardPadding),
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            btn.Click += (_, _) => Dispatcher.UIThread.Post(() => OpenChapter(ch));
            list.Children.Add(btn);
        }

        return new ScrollViewer { Content = list, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    }

    void UpdateChapterActions()
    {
        if (_speech.IsSpeaking)
        {
            _listenButton.Content = "Stop";
            BooksTheme.StyleButton(_listenButton, BooksButtonKind.Primary);
        }
        else
        {
            var text = PrepareSpeechMarkdown(_editor?.Text ?? string.Empty);
            _listenButton.Content = _speech.HasCachedAudio(text) ? "Listen ✓" : "Listen";
            BooksTheme.StyleButton(_listenButton, BooksButtonKind.Secondary);
        }

        var pending = HasPendingChanges();
        _saveButton.IsEnabled = pending && !_speech.IsSpeaking;
        _saveButton.Opacity = pending ? 1 : 0.45;
        if (_speech.IsSpeaking)
        {
            // Stop already owns Primary on Listen.
            BooksTheme.StyleButton(_saveButton, BooksButtonKind.Secondary);
            BooksTheme.StyleButton(_modeButton, BooksButtonKind.Secondary);
        }
        else if (pending)
        {
            BooksTheme.StyleButton(_saveButton, BooksButtonKind.Primary);
            BooksTheme.StyleButton(_modeButton, BooksButtonKind.Secondary);
        }
        else
        {
            BooksTheme.StyleButton(_saveButton, BooksButtonKind.Secondary);
            BooksTheme.StyleButton(_modeButton, BooksButtonKind.Primary);
        }

        _undoButton.IsEnabled = pending && !_speech.IsSpeaking;
        _undoButton.Opacity = pending ? 1 : 0.45;
        _undoButton.IsVisible = pending || _chapterMode == ChapterMode.Edit;
        _saveButton.IsVisible = true;
        _listenButton.IsEnabled = true;

        var idx = CurrentChapterIndex();
        var count = _openBook?.Chapters.Count ?? 0;
        _chapterMeta.Text = idx >= 0 && count > 0 ? $"{idx + 1} / {count}" : string.Empty;
        _prevChapterButton.IsEnabled = idx > 0 && !pending;
        _nextChapterButton.IsEnabled = idx >= 0 && idx < count - 1 && !pending;
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
        if (_screen == Screen.ReadAnything)
        {
            await OnReadAnythingListenAsync();
            return;
        }

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
            var text = PrepareSpeechMarkdown(_editor?.Text ?? string.Empty);
            var cached = _speech.HasCachedAudio(text);
            SetStatus(cached
                ? "Playing cached audio (Ava)…"
                : "Synthesizing first paragraph…");
            EnsureWake();
            UpdateChapterActions();
            await _speech.SpeakChapterAsync(text);
            SetStatus(cached ? "Finished (from cache)." : "Finished.");
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

    async Task OnReadAnythingListenAsync()
    {
        if (_speech.IsSpeaking)
        {
            _speech.Stop();
            ReleaseWake();
            SetStatus("Stopped.");
            RefreshReadAnythingListen();
            return;
        }

        if (!_readAnything.HasDocument)
        {
            SetStatus("Paste or open text to listen.");
            return;
        }

        try
        {
            var text = _readAnything.DocumentText;
            var cached = _speech.HasCachedAudio(text);
            SetStatus(cached
                ? "Playing cached audio (Ava)…"
                : "Synthesizing first paragraph…");
            EnsureWake();
            RefreshReadAnythingListen();
            await _speech.SpeakChapterAsync(text);
            SetStatus(cached ? "Finished (from cache)." : "Finished reading.");
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
            RefreshReadAnythingListen();
            RefreshChrome();
        }
    }

    void RefreshReadAnythingListen()
    {
        _readAnything.SetListening(
            _speech.IsSpeaking,
            _readAnything.HasDocument && _speech.HasCachedAudio(_readAnything.DocumentText));
    }

    async Task ImportSelectionAsync()
    {
        if (!_session.IsReviewMode)
        {
            SetStatus("Switch to Review mode first.");
            return;
        }

        var pasted = _pasteSelection.PastedText;
        var (ok, message, _) = ReviewSelectionImporter.Validate(pasted);
        if (!ok)
        {
            _pasteSelection.SetStatus(message);
            SetStatus(message);
            return;
        }

        _pasteSelection.SetStatus("Importing selection + synthesizing audio…");
        SetStatus("Importing selection + synthesizing audio…");
        var result = await _importer.ImportAsync(_session.WorkspaceRoot, pasted);
        if (!result.Ok)
        {
            _pasteSelection.SetStatus(result.Message);
            SetStatus(result.Message);
            return;
        }

        if (result.DirtyPaths is { Count: > 0 })
            _session.NoteDirtyPaths(result.DirtyPaths);

        _pasteSelection.SetStatus($"{result.Message} Pushing…");
        await _session.SaveCommitPushAsync($"Release selection: {result.Slug}.");
        _pasteSelection.SetStatus(_session.Status ?? result.Message);
        SetStatus(_session.Status ?? result.Message);
        ShowLibrary();
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
        AbandonDraft();
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
        AbandonDraft();
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
