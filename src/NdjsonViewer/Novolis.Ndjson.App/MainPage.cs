using System.Globalization;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls.Shapes;
using Novolis.Maui.GraphicalProfile;
using Novolis.Ndjson;
using Novolis.Ndjson.App.Viewer;
using Profile = Novolis.Maui.GraphicalProfile.GraphicalProfile;
#if WINDOWS
using Windows.Storage;
using WindowsDataPackageOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation;
using WindowsStandardDataFormats = Windows.ApplicationModel.DataTransfer.StandardDataFormats;
#endif

namespace Novolis.Ndjson.App;

public sealed class MainPage : ContentPage
{
    private readonly NdjsonDocumentSession _session;
    private readonly NdjsonFilePicker _picker;
    private readonly DocumentActivationInbox _activationInbox;
    private readonly Label _documentName;
    private readonly Label _documentMeta;
    private readonly Label _rangeLabel;
    private readonly Label _emptyLabel;
    private readonly Button _openButton;
    private readonly Button _refreshButton;
    private readonly Button _previousButton;
    private readonly Button _nextButton;
    private readonly Button _jumpButton;
    private readonly Entry _jumpEntry;
    private readonly Picker _takePicker;
    private readonly VerticalStackLayout _records;
    private readonly Border _welcomeCard;
    private readonly Border _viewerCard;
    private readonly ScrollView _welcomeScroll;
    private readonly Grid _body;
    private CancellationTokenSource? _activationCancellation;
    private ViewerState _state = new(0, 100, null, false, null);
    private bool _themeSubscribed;

    public MainPage(
        NdjsonDocumentSession session,
        NdjsonFilePicker picker,
        DocumentActivationInbox activationInbox)
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
            AutomationId = "NdjsonDocumentMeta",
            Text = "Choose a local .ndjson file to begin",
            FontFamily = Profile.FontFamily,
            FontSize = 12,
        };
        _rangeLabel = new Label
        {
            AutomationId = "NdjsonRange",
            Text = "No records",
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
        };
        _emptyLabel = new Label
        {
            Text = "Open an NDJSON file to inspect its records.",
            FontSize = 15,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
        };

        _openButton = CreateButton("Open", "NdjsonOpen", PickAndOpenAsync);
        _refreshButton = CreateButton("Refresh", "NdjsonRefresh", RefreshAsync);
        _previousButton = CreateButton("Previous", "NdjsonPrevious", PreviousAsync);
        _nextButton = CreateButton("Next", "NdjsonNext", NextAsync);
        _jumpButton = CreateButton("Jump", "NdjsonJump", JumpAsync);
        _jumpEntry = new Entry
        {
            AutomationId = "NdjsonJumpEntry",
            Placeholder = "Record number",
            Keyboard = Keyboard.Numeric,
            HorizontalOptions = LayoutOptions.Fill,
        };
        _takePicker = new Picker
        {
            AutomationId = "NdjsonTakePicker",
            Title = "Records per slice",
            ItemsSource = new[] { "50", "100", "250", "500", "1000" },
            SelectedItem = "100",
            HorizontalOptions = LayoutOptions.Fill,
        };
        _takePicker.SelectedIndexChanged += async (_, _) => await ChangeTakeAsync();

        _records = new VerticalStackLayout
        {
            AutomationId = "NdjsonRecords",
            Spacing = 8,
            Padding = new Thickness(12, 4, 12, 16),
        };

        var brandMark = new Border
        {
            WidthRequest = 52,
            HeightRequest = 52,
            Padding = 8,
            StrokeThickness = GraphicalProfileColors.Stroke,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(GraphicalProfileColors.MarkRadius) },
            Content = new Image
            {
                Source = "ndjsonmarkimage.svg",
                Aspect = Aspect.AspectFit,
            },
        };
        var brand = new VerticalStackLayout
        {
            Spacing = 1,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = "NOVOLIS NDJSON VIEWER",
                    FontAttributes = FontAttributes.Bold,
                    FontFamily = Profile.FontFamily,
                    FontSize = 13,
                    CharacterSpacing = 1.1,
                },
                _documentName,
                _documentMeta,
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
        header.Add(brandMark, 0, 0);
        header.Add(brand, 1, 0);
        header.Add(_openButton, 2, 0);

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

        var navigation = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 8,
        };
        navigation.Add(_previousButton, 0, 0);
        navigation.Add(_rangeLabel, 1, 0);
        navigation.Add(_nextButton, 2, 0);

        var controls = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(new GridLength(1.2, GridUnitType.Star)),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 8,
        };
        controls.Add(new Label
        {
            Text = "Records per slice",
            VerticalTextAlignment = TextAlignment.Center,
        }, 0, 0);
        controls.Add(_takePicker, 1, 0);
        controls.Add(_jumpEntry, 2, 0);
        controls.Add(_jumpButton, 3, 0);
        controls.Add(_refreshButton, 4, 0);

        var viewerLayout = new Grid
        {
            Padding = new Thickness(12, 12, 12, 0),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
            RowSpacing = 10,
        };
        viewerLayout.Add(new Label
        {
            Text = "RECORDS",
            FontAttributes = FontAttributes.Bold,
            FontSize = 10,
            CharacterSpacing = 1.4,
        }, 0, 0);
        viewerLayout.Add(navigation, 0, 1);
        viewerLayout.Add(controls, 0, 2);
        viewerLayout.Add(new ScrollView
        {
            AutomationId = "NdjsonRecordScroll",
            Content = _records,
            VerticalScrollBarVisibility = ScrollBarVisibility.Always,
        }, 0, 3);
        _viewerCard = CreateCard(viewerLayout);
        _viewerCard.Padding = new Thickness(0);

        _body = new Grid
        {
            Padding = new Thickness(16, 0, 16, 16),
        };
        _body.Add(_welcomeScroll);
        _body.Add(_viewerCard);

        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
        };
        layout.Add(header, 0, 0);
        layout.Add(_body, 0, 1);
        Content = layout;
        SizeChanged += (_, _) => ApplyShellLayout();

        _viewerCard.IsVisible = false;
        _previousButton.IsEnabled = false;
        _nextButton.IsEnabled = false;
        ApplyTheme();
    }

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

    protected override void OnDisappearing()
    {
        _activationCancellation?.Cancel();
        _activationCancellation?.Dispose();
        _activationCancellation = null;
        base.OnDisappearing();
    }

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
                || !System.IO.Path.GetExtension(file.Name).Equals(".ndjson", StringComparison.OrdinalIgnoreCase))
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
            _state = new ViewerState(0, _state.Take, null, false, null);
            await LoadSliceAsync(cancellationToken);
            _documentName.Text = _session.DisplayName ?? request.DisplayName;
            _viewerCard.IsVisible = true;
            _welcomeScroll.IsVisible = false;
            ApplyShellLayout();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            await ShowErrorAsync("Unable to open NDJSON", error).ConfigureAwait(false);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RefreshAsync()
    {
        if (_session.Document is null)
            return;

        var cancellationToken = CancellationToken.None;
        _state = ViewerNavigation.BeginRefresh(_state);
        SetBusy(true);
        try
        {
            await _session.RefreshAsync(cancellationToken);
            await LoadSliceAsync(cancellationToken);
        }
        catch (Exception error)
        {
            _state = ViewerNavigation.Fail(_state, error);
            await ShowErrorAsync("Unable to refresh NDJSON", error);
        }
        finally
        {
            _state = ViewerNavigation.CompleteRefresh(_state);
            SetBusy(false);
        }
    }

    private async Task PreviousAsync()
    {
        if (_session.Document is null)
            return;

        _state = ViewerNavigation.Previous(_state);
        await TryLoadSliceAsync(CancellationToken.None);
    }

    private async Task NextAsync()
    {
        if (_session.Document is null)
            return;

        _state = ViewerNavigation.Next(_state);
        await TryLoadSliceAsync(CancellationToken.None);
    }

    private async Task JumpAsync()
    {
        if (!long.TryParse(_jumpEntry.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var skip)
            || skip < 0)
        {
            await DisplayAlertAsync("Jump to record", "Enter a non-negative record number.", "OK");
            return;
        }

        _state = ViewerNavigation.Jump(_state, skip);
        await TryLoadSliceAsync(CancellationToken.None);
    }

    private async Task ChangeTakeAsync()
    {
        if (!int.TryParse(_takePicker.SelectedItem as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out var take))
            return;

        _state = ViewerNavigation.ChangeTake(_state, take);
        await TryLoadSliceAsync(CancellationToken.None);
    }

    private async Task LoadSliceAsync(CancellationToken cancellationToken)
    {
        var document = _session.Document;
        if (document is null)
            return;

        var slice = await document.ReadAsync(
            _state.Skip,
            _state.Take,
            cancellationToken);
        _state = _state with
        {
            Slice = slice,
            IsRefreshing = false,
            Error = null,
        };
        RenderSlice(document);
    }

    private async Task TryLoadSliceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await LoadSliceAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            _state = ViewerNavigation.Fail(_state, error);
            await ShowErrorAsync("Unable to read NDJSON", error);
        }
    }

    private void RenderSlice(INdjsonDocument document)
    {
        var slice = _state.Slice;
        _records.Children.Clear();
        if (slice is null || slice.Records.Count == 0)
        {
            _emptyLabel.IsVisible = true;
            _records.Children.Add(_emptyLabel);
        }
        else
        {
            _emptyLabel.IsVisible = false;
            foreach (var record in slice.Records)
                _records.Children.Add(CreateRecordView(new NdjsonRecordDisplay(record)));
        }

        var first = slice?.Records.FirstOrDefault();
        var last = slice?.Records.LastOrDefault();
        _rangeLabel.Text = first is null || last is null
            ? $"{_state.Skip:N0} · no records"
            : $"{first.Number:N0} – {last.Number:N0}";
        _documentMeta.Text = $"{FormatBytes(document.File.Length)} · {document.RecordCount:N0}"
            + (slice?.HasMore is true ? "+" : string.Empty)
            + " complete records";
        _previousButton.IsEnabled = slice?.HasPrevious is true;
        _nextButton.IsEnabled = slice?.HasMore is true;
        _viewerCard.IsVisible = true;
        _welcomeScroll.IsVisible = false;
        ApplyTheme();
    }

    private View CreateRecordView(NdjsonRecordDisplay record)
    {
        var details = new Editor
        {
            Text = record.Details,
            IsReadOnly = true,
            IsVisible = false,
            FontFamily = "Consolas",
            FontSize = 12,
            AutoSize = EditorAutoSizeOption.TextChanges,
            MaximumHeightRequest = 280,
        };
        Button? expand = null;
        expand = CreateButton("Expand", $"NdjsonExpand{record.Number}", () =>
        {
            details.IsVisible = !details.IsVisible;
            expand!.Text = details.IsVisible ? "Collapse" : "Expand";
            return Task.CompletedTask;
        });
        var copy = CreateButton("Copy", $"NdjsonCopy{record.Number}", async () =>
        {
            await Clipboard.Default.SetTextAsync(record.CopyText);
        });
        expand.Padding = new Thickness(10, 5);
        copy.Padding = new Thickness(10, 5);
        var actions = new HorizontalStackLayout
        {
            Spacing = 6,
            Children = { expand, copy },
        };
        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 8,
        };
        header.Add(new Label
        {
            Text = record.Number.ToString("N0", CultureInfo.InvariantCulture),
            FontFamily = "Consolas",
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center,
        }, 0, 0);
        header.Add(new Label
        {
            Text = record.Status,
            FontAttributes = FontAttributes.Bold,
            FontSize = 11,
            TextColor = Profile.Accent,
            VerticalTextAlignment = TextAlignment.Center,
        }, 1, 0);
        header.Add(actions, 2, 0);

        var content = new VerticalStackLayout
        {
            Spacing = 7,
            Children =
            {
                header,
                new Label
                {
                    Text = record.Preview,
                    FontFamily = "Consolas",
                    FontSize = 12,
                    LineBreakMode = LineBreakMode.WordWrap,
                },
                details,
            },
        };
        var border = CreateCard(content);
        border.Padding = new Thickness(12, 10);
        return border;
    }

    private async Task ShowErrorAsync(string title, Exception error) =>
        await DisplayAlertAsync(title, error.Message, "OK");

    private void SetBusy(bool isBusy)
    {
        _openButton.IsEnabled = !isBusy;
        _refreshButton.IsEnabled = !isBusy && _session.Document is not null;
        _previousButton.IsEnabled = !isBusy && _state.Slice?.HasPrevious is true;
        _nextButton.IsEnabled = !isBusy && _state.Slice?.HasMore is true;
        _jumpButton.IsEnabled = !isBusy;
        _takePicker.IsEnabled = !isBusy;
    }

    private void ApplyShellLayout()
    {
        _body.Padding = Width < 600
            ? new Thickness(0)
            : new Thickness(16, 0, 16, 16);
        _viewerCard.StrokeThickness = Width < 600 ? 0 : GraphicalProfileColors.Stroke;
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs args) =>
        ApplyTheme();

    private void ApplyTheme()
    {
        BackgroundColor = Profile.Background;
        _documentName.TextColor = Profile.Text;
        _documentMeta.TextColor = Profile.Muted;
        _rangeLabel.TextColor = Profile.Text;
        _emptyLabel.TextColor = Profile.Muted;
        _welcomeCard.Background = new SolidColorBrush(Profile.Surface);
        _welcomeCard.Stroke = new SolidColorBrush(Profile.Border);
        _viewerCard.Background = new SolidColorBrush(Profile.Surface);
        _viewerCard.Stroke = new SolidColorBrush(Profile.Border);
        _openButton.BackgroundColor = Profile.AccentFill;
        _openButton.TextColor = Profile.OnAccentFill;
        _refreshButton.BackgroundColor = Profile.ActionSoft;
        _refreshButton.TextColor = Profile.Text;
        _previousButton.BackgroundColor = Profile.Raised;
        _previousButton.TextColor = Profile.Text;
        _nextButton.BackgroundColor = Profile.Raised;
        _nextButton.TextColor = Profile.Text;
        _jumpButton.BackgroundColor = Profile.Action;
        _jumpButton.TextColor = Profile.OnAction;
        _jumpEntry.TextColor = Profile.Text;
        _jumpEntry.BackgroundColor = Profile.Raised;
        _takePicker.TextColor = Profile.Text;
        _takePicker.BackgroundColor = Profile.Raised;
    }

    private static string FormatBytes(long bytes)
    {
        var value = (double)Math.Max(0, bytes);
        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
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
