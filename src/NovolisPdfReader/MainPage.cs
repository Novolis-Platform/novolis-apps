using Microsoft.Maui.Controls.Shapes;
using Profile = Novolis.Maui.GraphicalProfile.GraphicalProfile;
using Novolis.Maui.PdfViewer;
using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Platform;

namespace NovolisPdfReader;

/// <summary>Branded shell around the reusable local PDF viewer.</summary>
public sealed class MainPage : ContentPage
{
    private readonly PdfViewer _viewer;
    private readonly PdfDocumentPicker _picker;
    private readonly PdfActivationInbox _activationInbox;
    private readonly PdfReaderDiagnosticsLog _diagnosticsLog;
    private readonly Editor _diagnosticsEditor;
    private readonly ScrollView _welcomeScroll;
    private readonly Border _welcomeCard;
    private readonly Border _viewerCard;
    private readonly Label _documentName;
    private readonly Label _documentMeta;
    private readonly Label _privacyNote;
    private readonly Button _openButton;
    private readonly Button _heroOpenButton;
    private readonly Button _registerButton;
    private CancellationTokenSource? _activationCancellation;
    private Task? _activationTask;
    private bool _themeSubscribed;

    /// <summary>Creates the reader shell.</summary>
    public MainPage(
        PdfViewer viewer,
        PdfDocumentPicker picker,
        PdfActivationInbox activationInbox,
        PdfReaderDiagnosticsLog diagnosticsLog)
    {
        _viewer = viewer;
        _picker = picker;
        _activationInbox = activationInbox;
        _diagnosticsLog = diagnosticsLog;
        _viewer.Error += OnViewerError;
        Title = "Novolis PDF Reader";
        _diagnosticsEditor = new Editor
        {
            AutomationId = "PdfReaderDiagnostics",
            IsReadOnly = true,
            FontFamily = "Consolas",
            FontSize = 11,
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 160,
            IsVisible = false,
        };

        _documentName = new Label
        {
            Text = "No document open",
            FontAttributes = FontAttributes.Bold,
            FontSize = 13,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1,
        };
        _documentMeta = new Label
        {
            Text = "Choose a local PDF to begin",
            FontSize = 12,
        };
        _privacyNote = new Label
        {
            Text = "Private by design · local files only",
            FontSize = 12,
            HorizontalTextAlignment = TextAlignment.Center,
        };

        _openButton = CreateButton("Open", "PdfReaderOpen", PickAndOpenAsync);
        _heroOpenButton = CreateButton(
            "Open a PDF document",
            "PdfReaderHeroOpen",
            PickAndOpenAsync);
        _registerButton = CreateButton(
            "Add to Open With",
            "PdfReaderRegisterAssociation",
            RegisterAssociationAsync);
        _registerButton.IsVisible = OperatingSystem.IsWindows();

        var brandMark = new Border
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
        var brand = new VerticalStackLayout
        {
            Spacing = 1,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = "NOVOLIS PDF READER",
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 18,
                    CharacterSpacing = 1.8,
                },
                _documentName,
                new Label
                {
                    Text = "A quiet home for your documents",
                    FontSize = 10,
                },
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

        _welcomeCard = CreateCard(
            new VerticalStackLayout
            {
                Spacing = 16,
                Children =
                {
                    CreateEyebrow("OFFLINE PDF READER"),
                    new Label
                    {
                        Text = "Read without sending your files away.",
                        FontAttributes = FontAttributes.Bold,
                        FontSize = 29,
                        LineBreakMode = LineBreakMode.WordWrap,
                    },
                    new Label
                    {
                        Text = "Open, search, navigate, and read local PDF documents with a bounded Novolis reader.",
                        FontSize = 15,
                        LineBreakMode = LineBreakMode.WordWrap,
                    },
                    new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Children =
                        {
                            CreateBadge("LOCAL", Profile.AccentFill),
                            CreateBadge("TYPE-SAFE", Profile.Action),
                            CreateBadge("PRIVATE", Profile.ActionSoft),
                        },
                    },
                    _heroOpenButton,
                    _registerButton,
                    _privacyNote,
                    _diagnosticsEditor,
                },
            });

        var viewerHeader = new VerticalStackLayout
        {
            Padding = new Thickness(20, 18, 20, 14),
            Spacing = 3,
            Children =
            {
                CreateEyebrow("OPEN DOCUMENT"),
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
        viewerLayout.Add(_viewer, 0, 1);
        _viewerCard = CreateCard(viewerLayout);
        _viewerCard.Padding = new Thickness(0);

        _welcomeScroll = new ScrollView
        {
            Content = _welcomeCard,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
        };
        var body = new Grid
        {
            Padding = new Thickness(16, 0, 16, 16),
        };
        body.Add(_welcomeScroll);
        body.Add(_viewerCard);

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

    /// <inheritdoc />
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
#if WINDOWS
        if (Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement element)
        {
            element.KeyDown -= OnWindowsKeyDown;
            element.KeyDown += OnWindowsKeyDown;
        }
#endif
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
        if (_activationTask is null or { IsCompleted: true })
        {
            _diagnosticsLog.Write("activation-listen", "Main page is listening for PDF activations.");
            _activationCancellation = new CancellationTokenSource();
            _activationTask = ConsumeActivationsAsync(_activationCancellation.Token);
        }
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        _activationCancellation?.Cancel();
        _activationCancellation?.Dispose();
        _activationCancellation = null;
        base.OnDisappearing();
    }

#if WINDOWS
    private void OnWindowsKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
    {
        if (_viewer.TryHandleKey(args.Key.ToString()))
            args.Handled = true;
    }
#endif

    private async Task PickAndOpenAsync()
    {
        try
        {
            _diagnosticsLog.Write("pick-start", "File picker requested.");
            if (await _picker.PickAsync().ConfigureAwait(false) is { } request)
            {
                _diagnosticsLog.Write("pick-selected", "Picker returned a PDF.", request);
                await OpenAsync(request, CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                _diagnosticsLog.Write("pick-cancelled", "Picker returned no file.");
            }
        }
        catch (Exception exception)
        {
            _diagnosticsLog.Write("pick-failed", "File picker failed.", exception: exception);
            await ShowOpenFailureAsync(exception);
        }
    }

    private async Task ConsumeActivationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var request in _activationInbox.ReadAllAsync(cancellationToken))
            {
                _diagnosticsLog.Write("activation-received", "OS activation dequeued.", request);
                await MainThread.InvokeOnMainThreadAsync(
                    () => OpenAsync(request, cancellationToken));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task OpenAsync(
        PdfOpenRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _diagnosticsLog.Write("open-start", "Viewer open requested.", request);
            await _viewer.OpenAsync(request, cancellationToken).ConfigureAwait(false);
            _diagnosticsLog.Write(
                "open-succeeded",
                $"Opened {_viewer.PageCount} page(s).",
                request,
                diagnostics: _viewer.Diagnostics);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _documentName.Text = _viewer.Session?.Document.Info.Title
                    ?? request.Descriptor.DisplayName;
                _documentMeta.Text = $"{_viewer.PageCount} pages · local document";
                _welcomeScroll.IsVisible = false;
                _viewerCard.IsVisible = true;
                _diagnosticsEditor.IsVisible = false;
                ApplyTheme();
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _diagnosticsLog.Write("open-cancelled", "Open was cancelled.", request);
        }
        catch (Exception exception)
        {
            _diagnosticsLog.Write(
                "open-failed",
                "Viewer could not open the PDF.",
                request,
                exception,
                _viewer.Diagnostics);
            await ShowOpenFailureAsync(exception);
        }
    }

    private void OnViewerError(object? sender, PdfViewerErrorEventArgs args) =>
        _diagnosticsLog.Write(
            "viewer-error",
            "PdfViewer raised Error.",
            exception: args.Exception,
            diagnostics: _viewer.Diagnostics);

    private async Task ShowOpenFailureAsync(Exception exception)
    {
        var summary = exception is PdfReaderException reader
            ? $"{reader.Diagnostic.Code}: {reader.Diagnostic.Message}"
            : $"{exception.GetType().Name}: {exception.Message}";
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            ShowWelcome();
            _diagnosticsEditor.Text =
                $"Log: {_diagnosticsLog.FilePath}{Environment.NewLine}{Environment.NewLine}{_diagnosticsLog.ReadTail()}";
            _diagnosticsEditor.IsVisible = true;
            ApplyTheme();
            await DisplayAlertAsync(
                "Unable to open PDF",
                $"{summary}{Environment.NewLine}{Environment.NewLine}Details were written to:{Environment.NewLine}{_diagnosticsLog.FilePath}",
                "OK");
        });
    }

    private Task RegisterAssociationAsync()
    {
#if WINDOWS
        var executable = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(executable))
        {
            new Novolis.Windows.Pdf.WindowsPdfFileAssociations().Register(executable);
            return ShowErrorAsync(
                "Open With updated",
                "Novolis PDF Reader is now available for PDF files in the current user account.");
        }
#endif
        return Task.CompletedTask;
    }

    private Task ShowErrorAsync(string title, string message) =>
        MainThread.InvokeOnMainThreadAsync(() => DisplayAlertAsync(title, message, "OK"));

    private void ShowWelcome()
    {
        _welcomeScroll.IsVisible = true;
        _viewerCard.IsVisible = false;
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs args) =>
        ApplyTheme();

    private void ApplyTheme()
    {
        BackgroundColor = Profile.Background;
        _documentName.TextColor = Profile.Text;
        _documentMeta.TextColor = Profile.Muted;
        _privacyNote.TextColor = Profile.Muted;
        _diagnosticsEditor.TextColor = Profile.Text;
        _diagnosticsEditor.BackgroundColor = Profile.Raised;
        _openButton.BackgroundColor = Profile.AccentFill;
        _openButton.TextColor = Profile.OnAccentFill;
        _heroOpenButton.BackgroundColor = Profile.Action;
        _heroOpenButton.TextColor = Profile.OnAction;
        _registerButton.BackgroundColor = Profile.ActionSoft;
        _registerButton.TextColor = Profile.Text;
        _welcomeCard.Background = new SolidColorBrush(Profile.Surface);
        _welcomeCard.Stroke = new SolidColorBrush(Profile.Border);
        _viewerCard.Background = new SolidColorBrush(Profile.Surface);
        _viewerCard.Stroke = new SolidColorBrush(Profile.Border);
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
            FontAttributes = FontAttributes.Bold,
            FontSize = 13,
            CornerRadius = 18,
            Padding = new Thickness(16, 9),
        };
        SemanticProperties.SetDescription(button, text);
        button.Clicked += async (_, _) => await action().ConfigureAwait(false);
        return button;
    }

    private static Border CreateCard(View content) =>
        new()
        {
            Padding = new Thickness(22),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(26) },
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
                TextColor = Profile.OnAction,
                FontAttributes = FontAttributes.Bold,
                FontSize = 10,
                CharacterSpacing = 0.8,
            },
        };
}
