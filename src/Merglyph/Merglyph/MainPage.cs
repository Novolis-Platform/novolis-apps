using System.Globalization;
using System.Linq;
using System.Text;
using Merglyph.Core;
using Novolis.Maui.Markdown;
using Microsoft.Maui.Controls.Shapes;
using Novolis.Maui.GraphicalProfile;

namespace Merglyph;

public sealed class MainPage : ContentPage
{
    private readonly DocumentSession _session;
    private readonly MarkdownDocumentPicker _picker;
    private readonly RecentDocumentStore _recentStore;
    private readonly DocumentActivationInbox _activationInbox;
    private readonly MarkdownView _viewer = new()
    {
        AutomationId = "DocumentViewer",
        Padding = new Thickness(3),
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
    private readonly Label _brandName = new()
    {
        Text = "MERGLYPH",
        FontAttributes = FontAttributes.Bold,
        FontSize = 18,
        CharacterSpacing = 2.2,
    };
    private readonly Label _brandTagline = new()
    {
        Text = "A quiet home for Markdown",
        FontSize = 10,
    };
    private readonly Label _welcomeEyebrow = new()
    {
        Text = "OFFLINE MARKDOWN READER",
        FontAttributes = FontAttributes.Bold,
        FontSize = 11,
        CharacterSpacing = 1.5,
    };
    private readonly Label _welcomeTitle = new()
    {
        Text = "Read the shape of your ideas.",
        FontAttributes = FontAttributes.Bold,
        FontSize = 30,
        LineBreakMode = LineBreakMode.WordWrap,
    };
    private readonly Label _welcomeDescription = new()
    {
        Text = "Merglyph keeps your documents close, clear, and beautifully rendered — including Mermaid diagrams.",
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
        Text = "OPEN DOCUMENT",
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
    private readonly Border _brandMarkFrame;
    private readonly Border _welcomeCard;
    private readonly Border _viewerCard;
    private readonly ScrollView _welcomeScroll;
    private readonly ContentView _bodyHost = new();
    private readonly Button _openButton;
    private readonly Button _heroOpenButton;
    private CancellationTokenSource? _activationCancellation;
    private Task? _activationTask;
    private bool _themeSubscribed;
    private bool _recentDocumentsLoaded;
    private IReadOnlyList<RecentDocument> _recentDocuments = [];

    public MainPage(
        DocumentSession session,
        MarkdownDocumentPicker picker,
        RecentDocumentStore recentStore,
        DocumentActivationInbox activationInbox)
    {
        _session = session;
        _picker = picker;
        _recentStore = recentStore;
        _activationInbox = activationInbox;

        Title = "Merglyph";
        _viewer.WebView.ExternalNavigationRequested += async (_, uri) =>
            await Launcher.Default.OpenAsync(uri);

        _openButton = new Button
        {
            AutomationId = "OpenDocument",
            Text = "Open",
            FontAttributes = FontAttributes.Bold,
            FontSize = 13,
            CornerRadius = 18,
            Padding = new Thickness(16, 9),
            HorizontalOptions = LayoutOptions.End,
        };
        _openButton.Clicked += async (_, _) => await PickAndOpenAsync();

        _heroOpenButton = new Button
        {
            AutomationId = "OpenDocumentHero",
            Text = "Open a Markdown file",
            FontAttributes = FontAttributes.Bold,
            FontSize = 14,
            CornerRadius = 22,
            Padding = new Thickness(20, 12),
            HorizontalOptions = LayoutOptions.Fill,
        };
        _heroOpenButton.Clicked += async (_, _) => await PickAndOpenAsync();

        _brandMarkFrame = new Border
        {
            WidthRequest = 52,
            HeightRequest = 52,
            Padding = 7,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(17) },
            Content = new Image
            {
                Source = "appiconfg.png",
                Aspect = Aspect.AspectFit,
            },
        };

        var brandCopy = new VerticalStackLayout
        {
            Spacing = 1,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                _brandName,
                _documentName,
                _brandTagline,
            },
        };

        var header = new Grid
        {
            Padding = new Thickness(18, 16, 18, 12),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 12,
        };
        header.Add(_brandMarkFrame, 0, 0);
        header.Add(brandCopy, 1, 0);
        header.Add(_openButton, 2, 0);

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
                            CreateBadge("OFFLINE", GraphicalProfile.AccentFill),
                            CreateBadge("MERMAID", GraphicalProfile.Action),
                            CreateBadge("PRIVATE", GraphicalProfile.ActionSoft),
                        },
                    },
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

        var viewerHeader = new VerticalStackLayout
        {
            Padding = new Thickness(20, 18, 20, 14),
            Spacing = 3,
            Children =
            {
                _documentEyebrow,
                _documentMeta,
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
        _viewer.HorizontalOptions = LayoutOptions.Fill;
        _viewer.VerticalOptions = LayoutOptions.Fill;
        viewerLayout.Add(_viewer, 0, 1);
        // WinUI WebView2 stays blank inside a rounded/clipped Border. Keep a rectangular card.
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

        var body = new Grid
        {
            Padding = new Thickness(16, 0, 16, 16),
            RowDefinitions = { new RowDefinition(GridLength.Star) },
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star) },
        };
        body.Add(_bodyHost, 0, 0);

        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
        };
        layout.Add(header, 0, 0);
        layout.Add(body, 0, 1);
        Content = layout;

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

            Display(document);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("Unable to open document", exception.Message, "OK");
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
            Display(document);
        else
            ShowWelcome();
    }

    private void Display(MarkdownDocument document)
    {
        _documentName.Text = document.Name.Value;
        _documentMeta.Text = "Markdown document • local file";
        _bodyHost.Content = _viewerCard;
        ApplyChromeTheme();
        _viewer.SourceDirectory = document.SourceDirectory;
        _viewer.Title = document.Name.Value;
        _viewer.Markdown = document.Content.Value;
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
        _documentMeta.Text = "Choose a local Markdown file to begin";
        _bodyHost.Content = _welcomeScroll;
        ApplyChromeTheme();
        _viewer.SourceDirectory = null;
        _viewer.Title = "Merglyph";
        _viewer.Markdown = string.Empty;
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
        _brandName.TextColor = text;
        _brandTagline.TextColor = muted;
        _documentName.TextColor = text;
        _welcomeEyebrow.TextColor = GraphicalProfile.Accent;
        _welcomeTitle.TextColor = text;
        _welcomeDescription.TextColor = muted;
        _privacyNote.TextColor = muted;
        _documentEyebrow.TextColor = GraphicalProfile.Accent;
        _documentMeta.TextColor = muted;
        _recentDocumentsTitle.TextColor = GraphicalProfile.Accent;
        _recentDocumentsEmpty.TextColor = muted;
        _openButton.BackgroundColor = GraphicalProfile.AccentFill;
        _openButton.TextColor = GraphicalProfile.OnAccentFill;
        _heroOpenButton.BackgroundColor = GraphicalProfile.Action;
        _heroOpenButton.TextColor = GraphicalProfile.OnAction;
        _brandMarkFrame.Background = new SolidColorBrush(GraphicalProfile.Raised);
        _brandMarkFrame.Stroke = new SolidColorBrush(border);
        _welcomeCard.Background = new SolidColorBrush(surface);
        _welcomeCard.Stroke = new SolidColorBrush(border);
        _viewerCard.Background = new SolidColorBrush(surface);
        _viewerCard.Stroke = new SolidColorBrush(border);
        RenderRecentDocuments();
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
