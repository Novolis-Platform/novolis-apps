using System.Globalization;
using System.Text;
using Merglyph.Core;
using Novolis.Commands.Navigation;
using Novolis.Maui.Activation;
using Novolis.Maui.Markdown;
using Microsoft.Maui.Controls.Shapes;
using Novolis.Maui.GraphicalProfile;
using Novolis.Maui.Navigation;

namespace Merglyph;

public sealed class MainPage : ContentPage
{
    private enum DocumentMode
    {
        Preview,
        Edit,
    }

    private readonly DocumentSession _session;
    private readonly MarkdownDocumentPicker _picker;
    private readonly MarkdownDocumentCreator _creator;
    private readonly RecentDocumentStore _recentStore;
    private readonly MauiActivationInbox<DocumentOpenRequest> _activationInbox;
    private readonly MarkdownView _viewer = new()
    {
        AutomationId = "DocumentViewer",
        Padding = new Thickness(3),
    };
    private readonly Editor _editor = new()
    {
        AutomationId = "DocumentEditor",
        FontFamily = "Consolas",
        FontSize = 15,
        AutoSize = EditorAutoSizeOption.Disabled,
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill,
    };
    private readonly Label _documentName = new()
    {
        AutomationId = "DocumentName",
        Text = "No document open",
        FontAttributes = FontAttributes.Bold,
        FontSize = 12,
        LineBreakMode = LineBreakMode.TailTruncation,
        MaxLines = 1,
        VerticalTextAlignment = TextAlignment.Center,
    };
    private readonly Label _welcomeEyebrow = new()
    {
        Text = "OFFLINE MARKDOWN EDITOR",
        FontAttributes = FontAttributes.Bold,
        FontSize = 11,
        CharacterSpacing = 1.5,
    };
    private readonly Label _welcomeTitle = new()
    {
        Text = "Write and read Markdown.",
        FontAttributes = FontAttributes.Bold,
        FontSize = 30,
        LineBreakMode = LineBreakMode.WordWrap,
    };
    private readonly Label _welcomeDescription = new()
    {
        Text = "Create or open a .md file, edit the source, preview Mermaid and formatted Markdown, then save.",
        FontSize = 15,
        LineBreakMode = LineBreakMode.WordWrap,
    };
    private readonly Label _privacyNote = new()
    {
        Text = "Private by design • local files only",
        FontSize = 12,
        HorizontalTextAlignment = TextAlignment.Center,
    };
    private readonly Label _documentEyebrow = new()
    {
        Text = "MARKDOWN DOCUMENT",
        FontAttributes = FontAttributes.Bold,
        FontSize = 10,
        CharacterSpacing = 1.4,
    };
    private readonly Label _documentMeta = new()
    {
        Text = "Choose a local Markdown file to begin",
        FontSize = 12,
    };
    private readonly Label _recentDocumentsTitle = new()
    {
        Text = "RECENT DOCUMENTS",
        FontAttributes = FontAttributes.Bold,
        FontSize = 10,
        CharacterSpacing = 1.4,
    };
    private readonly Label _recentDocumentsEmpty = new()
    {
        Text = "No recent documents yet.",
        FontSize = 13,
    };
    private readonly VerticalStackLayout _recentDocumentsList = new()
    {
        Spacing = 8,
    };
    private readonly Border _welcomeCard;
    private readonly Border _viewerCard;
    private readonly ScrollView _welcomeScroll;
    private readonly ContentView _bodyHost = new();
    private readonly ContentView _documentSurface = new();
    private readonly Button _heroOpenButton;
    private readonly Button _heroNewButton;
    private readonly Button _editModeButton;
    private readonly Button _previewModeButton;
    private readonly Button _saveButton;
    private readonly MobileNavigationShell _navigationShell;
    private CancellationTokenSource? _activationCancellation;
    private Task? _activationTask;
    private bool _themeSubscribed;
    private bool _recentDocumentsLoaded;
    private bool _suppressEditorChanged;
    private DocumentMode _mode = DocumentMode.Edit;
    private IReadOnlyList<RecentDocument> _recentDocuments = [];

    public MainPage(
        DocumentSession session,
        MarkdownDocumentPicker picker,
        MarkdownDocumentCreator creator,
        RecentDocumentStore recentStore,
        MauiActivationInbox<DocumentOpenRequest> activationInbox)
    {
        _session = session;
        _picker = picker;
        _creator = creator;
        _recentStore = recentStore;
        _activationInbox = activationInbox;

        Title = "Merglyph · Markdown";
        _viewer.WebView.ExternalNavigationRequested += async (_, uri) =>
            await Launcher.Default.OpenAsync(uri);
        _editor.TextChanged += OnEditorTextChanged;

        _heroOpenButton = CreateHeroButton(
            "OpenDocumentHero",
            "Open a Markdown file",
            async () => await PickAndOpenAsync());
        _heroNewButton = CreateHeroButton(
            "NewDocumentHero",
            "Create new Markdown",
            async () => await CreateAndOpenAsync());

        _editModeButton = CreateChromeButton("EditMode", "Edit", () => SetMode(DocumentMode.Edit));
        _previewModeButton = CreateChromeButton(
            "PreviewMode",
            "Preview",
            () => SetMode(DocumentMode.Preview));
        _saveButton = CreateChromeButton("SaveDocument", "Save", () => _ = SaveAsync());

        _welcomeCard = CreateCard(
            new VerticalStackLayout
            {
                Spacing = 17,
                Children =
                {
                    _welcomeEyebrow,
                    _welcomeTitle,
                    _welcomeDescription,
                    new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Children =
                        {
                            CreateBadge("EDIT", GraphicalProfile.AccentFill),
                            CreateBadge("PREVIEW", GraphicalProfile.Action),
                            CreateBadge("MERMAID", GraphicalProfile.ActionSoft),
                        },
                    },
                    _heroNewButton,
                    _heroOpenButton,
                    new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            _recentDocumentsTitle,
                            _recentDocumentsList,
                        },
                    },
                    _privacyNote,
                },
            });

        var modeRow = new HorizontalStackLayout
        {
            Spacing = 8,
            Children = { _editModeButton, _previewModeButton, _saveButton },
        };
        var viewerHeader = new VerticalStackLayout
        {
            Padding = new Thickness(20, 18, 20, 14),
            Spacing = 8,
            Children =
            {
                _documentName,
                _documentEyebrow,
                _documentMeta,
                modeRow,
            },
        };
        var viewerLayout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
        };
        viewerLayout.Add(viewerHeader, 0, 0);
        _documentSurface.HorizontalOptions = LayoutOptions.Fill;
        _documentSurface.VerticalOptions = LayoutOptions.Fill;
        viewerLayout.Add(_documentSurface, 0, 1);
        _viewerCard = new Border
        {
            Padding = new Thickness(0),
            StrokeThickness = 1,
            Content = viewerLayout,
        };

        _welcomeScroll = new ScrollView
        {
            Content = _welcomeCard,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
        };

        _bodyHost.HorizontalOptions = LayoutOptions.Fill;
        _bodyHost.VerticalOptions = LayoutOptions.Fill;
        _welcomeScroll.HorizontalOptions = LayoutOptions.Fill;
        _welcomeScroll.VerticalOptions = LayoutOptions.Fill;
        _viewerCard.HorizontalOptions = LayoutOptions.Fill;
        _viewerCard.VerticalOptions = LayoutOptions.Fill;
        _viewer.HorizontalOptions = LayoutOptions.Fill;
        _viewer.VerticalOptions = LayoutOptions.Fill;

        var body = new Grid
        {
            Padding = new Thickness(16, 0, 16, 16),
            RowDefinitions = { new RowDefinition(GridLength.Star) },
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star) },
        };
        body.Add(_bodyHost, 0, 0);

        var menuTree = new MobileMenuTree(
        [
            new MobileMenuNode(
                "document.new",
                "Create new Markdown",
                MobileMenuNodeKind.Action)
            {
                CommandId = "document.new",
            },
            new MobileMenuNode(
                "document.open",
                "Open Markdown file",
                MobileMenuNodeKind.Action)
            {
                CommandId = "document.open",
            },
            new MobileMenuNode(
                "document.edit",
                "Edit Markdown",
                MobileMenuNodeKind.Action)
            {
                CommandId = "document.edit",
            },
            new MobileMenuNode(
                "document.preview",
                "Preview Markdown",
                MobileMenuNodeKind.Action)
            {
                CommandId = "document.preview",
            },
            new MobileMenuNode(
                "document.save",
                "Save Markdown",
                MobileMenuNodeKind.Action)
            {
                CommandId = "document.save",
            },
        ]);
        var controller = new MobileNavigationController(menuTree);
        _navigationShell = new MobileNavigationShell(controller)
        {
            Title = "Merglyph",
            Body = body,
        };
        _navigationShell.ActivationRequested += OnNavigationActivationRequested;
        Content = _navigationShell;

        ShowWelcome();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_themeSubscribed && Application.Current is { } application)
        {
            application.RequestedThemeChanged += OnRequestedThemeChanged;
            _themeSubscribed = true;
            ApplyChromeTheme();
            if (_session.Current is null)
            {
                ShowWelcome();
                if (!_recentDocumentsLoaded)
                    _ = LoadRecentDocumentsAsync();
            }
        }

        if (_activationTask is null or { IsCompleted: true })
        {
            _activationCancellation = new CancellationTokenSource();
            _activationTask = ConsumeActivationsAsync(_activationCancellation.Token);
        }
    }

    protected override void OnDisappearing()
    {
        _activationCancellation?.Cancel();
        _activationCancellation?.Dispose();
        _activationCancellation = null;
        base.OnDisappearing();
    }

    /// <summary>Closes the menu first, then delegates to the platform page stack.</summary>
    protected override bool OnBackButtonPressed() =>
        _navigationShell.TryHandleBack() || base.OnBackButtonPressed();

    private async Task PickAndOpenAsync()
    {
        try
        {
            var request = await _picker.PickAsync();
            if (request is not null)
                await OpenAsync(request, CancellationToken.None);
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("Unable to open document", exception.Message, "OK");
        }
    }

    private async Task CreateAndOpenAsync()
    {
        try
        {
            var request = await _creator.CreateAsync();
            await OpenAsync(request, CancellationToken.None);
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("Unable to create document", exception.Message, "OK");
        }
    }

    private void OnNavigationActivationRequested(
        object? sender,
        MobileMenuActivationEventArgs args)
    {
        switch (args.CommandId)
        {
            case "document.new":
                _ = CreateAndOpenAsync();
                break;
            case "document.open":
                _ = PickAndOpenAsync();
                break;
            case "document.edit":
                if (_session.Current is not null)
                    SetMode(DocumentMode.Edit);
                break;
            case "document.preview":
                if (_session.Current is not null)
                    SetMode(DocumentMode.Preview);
                break;
            case "document.save":
                _ = SaveAsync();
                break;
        }
    }

    private async Task ConsumeActivationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var request in _activationInbox.ReadAllAsync(cancellationToken))
            {
                await MainThread.InvokeOnMainThreadAsync(
                    () => OpenAsync(request, cancellationToken));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task OpenAsync(DocumentOpenRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var document = await _session.OpenAsync(request, cancellationToken);
            try
            {
                _recentDocuments = await _recentStore.RememberAsync(document, cancellationToken);
                _recentDocumentsLoaded = true;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            Display(document, preferredMode: DocumentMode.Edit);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("Unable to open document", exception.Message, "OK");
        }
    }

    private async Task SaveAsync()
    {
        if (_session.Current is null)
        {
            await DisplayAlertAsync("Nothing to save", "Open or create a Markdown document first.", "OK");
            return;
        }

        CommitEditorToSession();
        try
        {
            if (!_session.CanSave)
            {
                var request = await _creator.CreateAsync(_session.Current.Content.Value);
                await using var stream = await request.OpenReadAsync(CancellationToken.None);
                var document = await new MarkdownDocumentReader().ReadAsync(
                    request.Name,
                    stream,
                    CancellationToken.None);
                document = document with
                {
                    SourceDirectory = request.SourceDirectory,
                    DisplayPath = request.DisplayPath,
                    Content = _session.Current.Content,
                };
                _session.BindSave(request, document);
            }
            else
            {
                await _session.SaveAsync();
            }

            try
            {
                _recentDocuments = await _recentStore.RememberAsync(_session.Current!);
                _recentDocumentsLoaded = true;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            RefreshDocumentChrome();
            await DisplayAlertAsync("Saved", $"Saved {_session.Current!.Name.Value}.", "OK");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("Unable to save document", exception.Message, "OK");
        }
    }

    private async Task LoadRecentDocumentsAsync()
    {
        try
        {
            var recentDocuments = await _recentStore.LoadAsync();
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (_session.Current is not null)
                    return;

                _recentDocuments = recentDocuments;
                RenderRecentDocuments();
            });
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        finally
        {
            _recentDocumentsLoaded = true;
        }
    }

    private async Task OpenRecentAsync(RecentDocument recentDocument)
    {
        var request = new DocumentOpenRequest(
            new DocumentName(recentDocument.Name),
            _ => ValueTask.FromResult<Stream>(
                new MemoryStream(Encoding.UTF8.GetBytes(recentDocument.Content), writable: false)));
        await OpenAsync(request, CancellationToken.None);
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs args)
    {
        if (_session.Current is { } document)
            Display(document, _mode);
        else
            ShowWelcome();
    }

    private void OnEditorTextChanged(object? sender, TextChangedEventArgs args)
    {
        if (_suppressEditorChanged || _session.Current is null)
            return;
        _session.SetContent(args.NewTextValue ?? string.Empty);
        RefreshDocumentChrome();
    }

    private void Display(MarkdownDocument document, DocumentMode preferredMode)
    {
        _mode = preferredMode;
        _suppressEditorChanged = true;
        _editor.Text = document.Content.Value;
        _suppressEditorChanged = false;
        RefreshDocumentChrome();
        _bodyHost.Content = _viewerCard;
        ApplyChromeTheme();
        ApplyDocumentSurface();
    }

    private void SetMode(DocumentMode mode)
    {
        if (_session.Current is null)
            return;

        CommitEditorToSession();
        _mode = mode;
        ApplyDocumentSurface();
        RefreshDocumentChrome();
        ApplyChromeTheme();
    }

    private void CommitEditorToSession()
    {
        if (_session.Current is null)
            return;
        _session.SetContent(_editor.Text ?? string.Empty);
    }

    private void ApplyDocumentSurface()
    {
        if (_session.Current is null)
            return;

        if (_mode == DocumentMode.Edit)
        {
            _documentSurface.Content = _editor;
            return;
        }

        _viewer.SourceDirectory = _session.Current.SourceDirectory;
        _viewer.Title = _session.Current.Name.Value;
        _viewer.Markdown = _session.Current.Content.Value;
        _documentSurface.Content = _viewer;
        if (Environment.GetCommandLineArgs().Contains("--preview", StringComparer.OrdinalIgnoreCase))
            _ = OpenFirstPreviewWhenReadyAsync();
    }

    private async Task OpenFirstPreviewWhenReadyAsync()
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            await Task.Delay(250).ConfigureAwait(true);
            if (_viewer.TryOpenPreview(0))
                return;
        }
    }

    private void ShowWelcome()
    {
        _documentName.Text = "No document open";
        _documentMeta.Text = "Create a new note or open a local Markdown file";
        _navigationShell.Title = "Merglyph";
        _bodyHost.Content = _welcomeScroll;
        ApplyChromeTheme();
        _viewer.SourceDirectory = null;
        _viewer.Title = "Merglyph";
        _viewer.Markdown = string.Empty;
        _suppressEditorChanged = true;
        _editor.Text = string.Empty;
        _suppressEditorChanged = false;
    }

    private void RefreshDocumentChrome()
    {
        if (_session.Current is not { } document)
            return;

        var dirty = _session.IsDirty ? "• " : string.Empty;
        _documentName.Text = dirty + document.Name.Value;
        var location = !string.IsNullOrWhiteSpace(document.DisplayPath)
            ? document.DisplayPath
            : document.SourceDirectory;
        var modeLabel = _mode == DocumentMode.Edit ? "Editing source" : "Reading preview";
        _documentMeta.Text = string.IsNullOrWhiteSpace(location)
            ? $"{modeLabel} • local Markdown"
            : $"{modeLabel} • {location}";
        _navigationShell.Title = $"{dirty}Merglyph · {document.Name.Value}";
        _saveButton.Text = _session.CanSave
            ? (_session.IsDirty ? "Save*" : "Save")
            : "Save As";
        StyleModeButtons();
    }

    private void ApplyChromeTheme()
    {
        var theme = CurrentTheme();
        _viewer.Theme = theme;
        var background = GraphicalProfile.Background;
        var surface = GraphicalProfile.Surface;
        var border = GraphicalProfile.Border;
        var text = GraphicalProfile.Text;
        var muted = GraphicalProfile.Muted;

        BackgroundColor = background;
        _documentName.TextColor = text;
        _welcomeEyebrow.TextColor = GraphicalProfile.Accent;
        _welcomeTitle.TextColor = text;
        _welcomeDescription.TextColor = muted;
        _privacyNote.TextColor = muted;
        _documentEyebrow.TextColor = GraphicalProfile.Accent;
        _documentMeta.TextColor = muted;
        _recentDocumentsTitle.TextColor = GraphicalProfile.Accent;
        _recentDocumentsEmpty.TextColor = muted;
        _heroNewButton.BackgroundColor = GraphicalProfile.AccentFill;
        _heroNewButton.TextColor = GraphicalProfile.Text;
        _heroOpenButton.BackgroundColor = GraphicalProfile.Action;
        _heroOpenButton.TextColor = GraphicalProfile.OnAction;
        _editor.TextColor = text;
        _editor.BackgroundColor = GraphicalProfile.Raised;
        _welcomeCard.Background = new SolidColorBrush(surface);
        _welcomeCard.Stroke = new SolidColorBrush(border);
        _viewerCard.Background = new SolidColorBrush(surface);
        _viewerCard.Stroke = new SolidColorBrush(border);
        StyleModeButtons();
        RenderRecentDocuments();
    }

    private void StyleModeButtons()
    {
        StyleToggle(_editModeButton, selected: _mode == DocumentMode.Edit);
        StyleToggle(_previewModeButton, selected: _mode == DocumentMode.Preview);
        _saveButton.BackgroundColor = GraphicalProfile.AccentFill;
        _saveButton.TextColor = GraphicalProfile.Text;
    }

    private static void StyleToggle(Button button, bool selected)
    {
        button.BackgroundColor = selected
            ? GraphicalProfile.Action
            : GraphicalProfile.Raised;
        button.TextColor = selected
            ? GraphicalProfile.OnAction
            : GraphicalProfile.Text;
    }

    private void RenderRecentDocuments()
    {
        _recentDocumentsList.Clear();
        if (_recentDocuments.Count == 0)
        {
            _recentDocumentsList.Children.Add(_recentDocumentsEmpty);
            return;
        }

        foreach (var recentDocument in _recentDocuments.Take(RecentDocumentStore.MaximumRecentDocuments))
        {
            var document = recentDocument;
            var button = new Button
            {
                Text = $"{document.Name}\nOpened {document.LastOpenedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)}",
                FontSize = 13,
                HorizontalOptions = LayoutOptions.Fill,
                Padding = new Thickness(14, 10),
                CornerRadius = 14,
                BackgroundColor = GraphicalProfile.Raised,
                TextColor = GraphicalProfile.Text,
            };
            button.Clicked += async (_, _) => await OpenRecentAsync(document);
            _recentDocumentsList.Children.Add(button);
        }
    }

    private static Button CreateHeroButton(string automationId, string text, Func<Task> onClick)
    {
        var button = new Button
        {
            AutomationId = automationId,
            Text = text,
            FontAttributes = FontAttributes.Bold,
            FontSize = 14,
            CornerRadius = 22,
            Padding = new Thickness(20, 12),
            HorizontalOptions = LayoutOptions.Fill,
        };
        button.Clicked += async (_, _) => await onClick();
        return button;
    }

    private static Button CreateChromeButton(string automationId, string text, Action onClick)
    {
        var button = new Button
        {
            AutomationId = automationId,
            Text = text,
            FontAttributes = FontAttributes.Bold,
            FontSize = 13,
            CornerRadius = 16,
            Padding = new Thickness(14, 8),
        };
        button.Clicked += (_, _) => onClick();
        return button;
    }

    private static Border CreateCard(View content) =>
        new()
        {
            Padding = new Thickness(22),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(18) },
            Content = content,
        };

    private static Border CreateBadge(string text, Color color) =>
        new()
        {
            Padding = new Thickness(10, 6),
            Background = new SolidColorBrush(color),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) },
            Content = new Label
            {
                Text = text,
                TextColor = GraphicalProfile.OnAction,
                FontAttributes = FontAttributes.Bold,
                FontSize = 10,
                CharacterSpacing = 0.8,
            },
        };

    private static MarkdownViewTheme CurrentTheme() =>
        Application.Current?.RequestedTheme is AppTheme.Light
            ? MarkdownViewTheme.GitHubLight
            : MarkdownViewTheme.GitHubDark;
}
