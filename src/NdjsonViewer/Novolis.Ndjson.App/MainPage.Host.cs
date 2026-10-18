using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls.Shapes;
using Novolis.Maui.Activation;
using Novolis.Commands.Navigation;
using Novolis.Maui.GraphicalProfile;
using Novolis.Maui.Ndjson;
using Novolis.Maui.Navigation;
using Profile = Novolis.Maui.GraphicalProfile.GraphicalProfile;
#if WINDOWS
using Windows.Storage;
using WindowsDataPackageOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation;
using WindowsStandardDataFormats = Windows.ApplicationModel.DataTransfer.StandardDataFormats;
#endif

namespace Novolis.Ndjson.App;

/// <summary>Thin file gateway around the reusable MAUI NDJSON slice view.</summary>
public sealed class MainPage : ContentPage
{
    private readonly NdjsonDocumentSession _session;
    private readonly NdjsonFilePicker _picker;
    private readonly MauiActivationInbox<NdjsonOpenRequest> _activationInbox;
    private readonly NdjsonSliceView _sliceView;
    private readonly Label _documentName;
    private readonly Label _documentMeta;
    private readonly Border _welcomeCard;
    private readonly ScrollView _welcomeScroll;
    private readonly Grid _body;
    private readonly MobileNavigationShell _navigationShell;
    private CancellationTokenSource? _activationCancellation;
    private bool _themeSubscribed;

    /// <summary>Creates the host page and wires platform file gateways.</summary>
    public MainPage(
        NdjsonDocumentSession session,
        NdjsonFilePicker picker,
        MauiActivationInbox<NdjsonOpenRequest> activationInbox)
    {
        _session = session;
        _picker = picker;
        _activationInbox = activationInbox;
        Title = "Novolis NDJSON Viewer";

        _documentName = new Label
        {
            AutomationId = "NdjsonDocumentName",
            Text = "No document open",
            FontAttributes = FontAttributes.Bold,
            FontFamily = Profile.FontFamily,
            FontSize = 18,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1,
            VerticalTextAlignment = TextAlignment.Center,
        };
        _documentMeta = new Label
        {
            AutomationId = "NdjsonDocumentHostMeta",
            Text = "Choose a local .ndjson or .jsonl file to begin",
            FontFamily = Profile.FontFamily,
            FontSize = 12,
        };
        _sliceView = new NdjsonSliceView
        {
            IsVisible = false,
            RefreshDocumentAsync = RefreshDocumentAsync,
            ErrorHandler = ShowErrorAsync,
        };

        var heroOpen = CreateButton("Open an NDJSON file", "NdjsonHeroOpen", PickAndOpenAsync);
        heroOpen.HorizontalOptions = LayoutOptions.Fill;
        var welcomeContent = new VerticalStackLayout
        {
            Spacing = 16,
            Children =
            {
                CreateEyebrow("LOCAL DIAGNOSTIC VIEWER"),
                new Label
                {
                    Text = "Inspect enormous JSONL files without loading them whole.",
                    FontAttributes = FontAttributes.Bold,
                    FontFamily = Profile.FontFamily,
                    FontSize = 29,
                    LineBreakMode = LineBreakMode.WordWrap,
                },
                new Label
                {
                    Text = "Navigate bounded record slices, keep malformed lines visible, and refresh files that are still growing.",
                    FontSize = 15,
                    LineBreakMode = LineBreakMode.WordWrap,
                },
                heroOpen,
                new Label
                {
                    Text = "Private by design · local files only",
                    FontSize = 12,
                    HorizontalTextAlignment = TextAlignment.Center,
                },
            },
        };
        _welcomeCard = CreateCard(welcomeContent);
        _welcomeScroll = new ScrollView
        {
            AutomationId = "NdjsonWelcome",
            Content = _welcomeCard,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
        };

        _body = new Grid
        {
            Padding = new Thickness(16, 0, 16, 16),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
        };
        var documentInfo = new VerticalStackLayout
        {
            Spacing = 2,
            Padding = new Thickness(0, 8, 0, 8),
            Children =
            {
                _documentName,
                _documentMeta,
            },
        };
        _body.Add(documentInfo, 0, 0);
        _body.Add(_welcomeScroll, 0, 1);
        _body.Add(_sliceView, 0, 1);

        var menuTree = new MobileMenuTree(
        [
            new MobileMenuNode(
                "ndjson.open",
                "Open NDJSON or JSONL file",
                MobileMenuNodeKind.Action)
            {
                CommandId = "ndjson.open",
            },
        ]);
        _navigationShell = new MobileNavigationShell(new MobileNavigationController(menuTree))
        {
            Title = "NDJSON Viewer",
            Body = _body,
        };
        _navigationShell.ActivationRequested += OnNavigationActivationRequested;
        Content = _navigationShell;
        SizeChanged += (_, _) => ApplyShellLayout();
        ApplyTheme();
    }

    private void OnNavigationActivationRequested(
        object? sender,
        MobileMenuActivationEventArgs args)
    {
        if (args.CommandId == "ndjson.open")
            _ = PickAndOpenAsync();
    }

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_themeSubscribed && Application.Current is { } application)
        {
            application.RequestedThemeChanged += OnRequestedThemeChanged;
            _themeSubscribed = true;
        }

        ApplyTheme();
        if (_activationCancellation is null)
        {
            _activationCancellation = new CancellationTokenSource();
            _ = ConsumeActivationsAsync(_activationCancellation.Token);
        }
    }

    /// <inheritdoc />
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
#if WINDOWS
        if (Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement element)
        {
            element.AllowDrop = true;
            element.DragOver -= OnWindowsDragOver;
            element.DragOver += OnWindowsDragOver;
            element.Drop -= OnWindowsDrop;
            element.Drop += OnWindowsDrop;
        }
#endif
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        _activationCancellation?.Cancel();
        _activationCancellation?.Dispose();
        _activationCancellation = null;
        base.OnDisappearing();
    }

    /// <summary>Closes the menu before allowing the platform page stack to navigate back.</summary>
    protected override bool OnBackButtonPressed() =>
        _navigationShell.TryHandleBack() || base.OnBackButtonPressed();

#if WINDOWS
    private static void OnWindowsDragOver(object sender, Microsoft.UI.Xaml.DragEventArgs args) =>
        args.AcceptedOperation = WindowsDataPackageOperation.Copy;

    private async void OnWindowsDrop(object sender, Microsoft.UI.Xaml.DragEventArgs args)
    {
        try
        {
            if (!args.DataView.Contains(WindowsStandardDataFormats.StorageItems))
                return;

            var items = await args.DataView.GetStorageItemsAsync();
            if (items.OfType<StorageFile>().FirstOrDefault() is not { } file
                || !IsSupportedExtension(file.Name))
            {
                return;
            }

            var request = !string.IsNullOrWhiteSpace(file.Path) && File.Exists(file.Path)
                ? NdjsonOpenRequest.FromFile(new FileInfo(file.Path))
                : NdjsonOpenRequest.FromStream(
                    file.Name,
                    async cancellationToken =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var randomAccessStream = await file.OpenAsync(FileAccessMode.Read);
                        return randomAccessStream.AsStreamForRead();
                    });
            await OpenAsync(request);
        }
        catch (Exception error)
        {
            await ShowErrorAsync("Unable to open dropped NDJSON", error);
        }
    }
#endif

    private async Task PickAndOpenAsync()
    {
        try
        {
            if (await _picker.PickAsync() is { } request)
                await OpenAsync(request);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error)
        {
            await ShowErrorAsync("Unable to open NDJSON", error);
        }
    }

    private async Task ConsumeActivationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var request in _activationInbox.ReadAllAsync(cancellationToken))
                await MainThread.InvokeOnMainThreadAsync(() => OpenAsync(request, cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task OpenAsync(
        NdjsonOpenRequest request,
        CancellationToken cancellationToken = default)
    {
        SetBusy(true);
        try
        {
            await _session.OpenAsync(request, cancellationToken);
            if (_session.Document is null)
                throw new InvalidOperationException("The document did not open.");

            await _sliceView.OpenAsync(_session.Document, request.DisplayName, cancellationToken);
            _documentName.Text = _session.DisplayName ?? request.DisplayName;
            _documentMeta.Text = "NDJSON / JSONL · local file · bounded slices";
            _navigationShell.Title = $"NDJSON Viewer · {_documentName.Text}";
            _sliceView.IsVisible = true;
            _welcomeScroll.IsVisible = false;
            ApplyShellLayout();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            await ShowErrorAsync("Unable to open NDJSON", error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<Novolis.IO.Ndjson.INdjsonDocument?> RefreshDocumentAsync(
        CancellationToken cancellationToken)
    {
        await _session.RefreshAsync(cancellationToken);
        return _session.Document;
    }

    private Task ShowErrorAsync(string title, Exception error) =>
        DisplayAlertAsync(title, error.Message, "OK");

    private void SetBusy(bool isBusy) =>
        _navigationShell.IsEnabled = !isBusy;

    private static bool IsSupportedExtension(string name)
    {
        var extension = System.IO.Path.GetExtension(name);
        return extension.Equals(".ndjson", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jsonl", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyShellLayout()
    {
        _body.Padding = Width < 600
            ? new Thickness(0)
            : new Thickness(16, 0, 16, 16);
        _sliceView.ApplyTheme();
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs args) =>
        ApplyTheme();

    private void ApplyTheme()
    {
        BackgroundColor = Profile.Background;
        _documentName.TextColor = Profile.Text;
        _documentMeta.TextColor = Profile.Muted;
        _welcomeCard.Background = new SolidColorBrush(Profile.Surface);
        _welcomeCard.Stroke = new SolidColorBrush(Profile.Border);
        _sliceView.ApplyTheme();
    }

    private static Label CreateEyebrow(string text) =>
        new()
        {
            Text = text,
            FontAttributes = FontAttributes.Bold,
            FontSize = 10,
            CharacterSpacing = 1.4,
            TextColor = Profile.Accent,
        };

    private static Button CreateButton(
        string text,
        string automationId,
        Func<Task> action)
    {
        var button = new Button
        {
            Text = text,
            AutomationId = automationId,
            FontFamily = Profile.FontFamily,
            FontAttributes = FontAttributes.Bold,
            FontSize = GraphicalProfileColors.ButtonSize,
            BackgroundColor = Profile.Raised,
            TextColor = Profile.Text,
            BorderColor = Profile.Border,
            BorderWidth = GraphicalProfileColors.Stroke,
            CornerRadius = (int)GraphicalProfileColors.PrimaryRadius,
            MinimumHeightRequest = GraphicalProfileColors.TouchTarget,
            Padding = new Thickness(16, 9),
        };
        SemanticProperties.SetDescription(button, text);
        button.Clicked += async (_, _) => await action().ConfigureAwait(false);
        return button;
    }

    private static Border CreateCard(View content) =>
        new()
        {
            Padding = new Thickness(GraphicalProfileColors.CardPadding),
            StrokeThickness = GraphicalProfileColors.Stroke,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(GraphicalProfileColors.CardRadius) },
            Content = content,
        };
}
