using Merglyph.Core;
using Novolis.Maui.Markdown;

namespace Merglyph;

public sealed class MainPage : ContentPage
{
    private readonly DocumentSession _session;
    private readonly MarkdownDocumentPicker _picker;
    private readonly DocumentActivationInbox _activationInbox;
    private readonly MarkdownView _viewer = new();
    private readonly Label _documentName = new()
    {
        AutomationId = "DocumentName",
        Text = "No document open",
        VerticalTextAlignment = TextAlignment.Center,
    };
    private CancellationTokenSource? _activationCancellation;
    private Task? _activationTask;
    private bool _themeSubscribed;

    public MainPage(
        DocumentSession session,
        MarkdownDocumentPicker picker,
        DocumentActivationInbox activationInbox)
    {
        _session = session;
        _picker = picker;
        _activationInbox = activationInbox;

        Title = "Merglyph";
        _viewer.WebView.ExternalNavigationRequested += async (_, uri) =>
            await Launcher.Default.OpenAsync(uri);

        var open = new Button
        {
            AutomationId = "OpenDocument",
            Text = "Open",
        };
        open.Clicked += async (_, _) => await PickAndOpenAsync();

        var header = new Grid
        {
            Padding = new Thickness(12, 8),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
            },
            ColumnSpacing = 12,
        };
        header.Add(open, 0, 0);
        header.Add(_documentName, 1, 0);

        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
        };
        layout.Add(header, 0, 0);
        layout.Add(_viewer, 0, 1);
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
                ShowWelcome();
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
        ApplyChromeTheme();
        _viewer.Title = document.Name.Value;
        _viewer.Html = null;
        _viewer.Markdown = document.Content.Value;
    }

    private void ShowWelcome()
    {
        ApplyChromeTheme();
        _viewer.Html = null;
        _viewer.Title = "Merglyph";
        _viewer.Markdown = """
            # Merglyph

            Open a Markdown file. Mermaid fences are rendered locally.
            """;
    }

    private void ApplyChromeTheme()
    {
        var theme = CurrentTheme();
        _viewer.Theme = theme;
        var dark = theme is not MarkdownViewTheme.GitHubLight;
        BackgroundColor = dark ? Color.FromArgb("#0d1117") : Colors.White;
        _documentName.TextColor = dark ? Color.FromArgb("#e6edf3") : Color.FromArgb("#24292f");
    }

    private static MarkdownViewTheme CurrentTheme() =>
        Application.Current?.RequestedTheme is AppTheme.Light
            ? MarkdownViewTheme.GitHubLight
            : MarkdownViewTheme.GitHubDark;
}
