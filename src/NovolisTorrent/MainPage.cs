using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
using Novolis.Maui.GraphicalProfile;
using Novolis.Transports.Torrent.Search;
using Novolis.Transports.Torrent.Trackers;
using Profile = Novolis.Maui.GraphicalProfile.GraphicalProfile;

namespace Novolis.Torrent;

public sealed class MainPage : ContentPage
{
    private const string SearchRoute = "search";
    private const string TransfersRoute = "transfers";
    private const string TrackersRoute = "trackers";
    private const string SettingsRoute = "settings";
    private readonly HttpClient _httpClient;
    private readonly TorrentTransferService _transferService;
    private readonly TorrentTrackerCatalog _trackerCatalog;
    private readonly Entry _endpoint;
    private readonly Entry _apiKey;
    private readonly Entry _term;
    private readonly Entry _minimumSeeders;
    private readonly Entry _minimumLeechers;
    private readonly Entry _minimumRatio;
    private readonly Entry _minimumAvailability;
    private readonly Entry _minimumSizeMb;
    private readonly Picker _sort;
    private readonly Label _searchStatus;
    private readonly Label _trackerStatus;
    private readonly Label _transferStatus;
    private readonly Label _pageTitle;
    private readonly CollectionView _results;
    private readonly CollectionView _trackerResults;
    private readonly CollectionView _transferResults;
    private readonly ObservableCollection<TorrentSearchResult> _searchItems = [];
    private readonly ObservableCollection<string> _trackerItems = [];
    private readonly ObservableCollection<TorrentTransferItem> _transferItems = [];
    private readonly Dictionary<string, Button> _routeButtons = [];
    private readonly Dictionary<string, View> _routes = [];
    private IReadOnlyList<Uri> _trackerUris = [];
    private bool _themeSubscribed;
    private bool _busy;

    public MainPage(
        HttpClient httpClient,
        TorrentTransferService transferService)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _transferService = transferService
            ?? throw new ArgumentNullException(nameof(transferService));
        _trackerCatalog = new TorrentTrackerCatalog(_httpClient);

        Title = "Novolis Torrent";
        BackgroundColor = Profile.Background;

        _endpoint = CreateEntry(
            "Torznab API endpoint",
            Preferences.Default.Get("torrent.endpoint", string.Empty),
            "TorrentEndpoint");
        _apiKey = CreateEntry("API key (optional)", string.Empty, "TorrentApiKey");
        _term = CreateEntry("Search for a title, release, or file", string.Empty, "TorrentSearchTerm");
        _term.ReturnType = ReturnType.Search;
        _term.Completed += (_, _) => _ = SearchAsync();
        _minimumSeeders = CreateEntry("Min seeders", string.Empty, "TorrentMinSeeders");
        _minimumLeechers = CreateEntry("Min leechers", string.Empty, "TorrentMinLeechers");
        _minimumRatio = CreateEntry("Min ratio", string.Empty, "TorrentMinRatio");
        _minimumAvailability = CreateEntry(
            "Min availability",
            string.Empty,
            "TorrentMinAvailability");
        _minimumSizeMb = CreateEntry("Min size MB", string.Empty, "TorrentMinSizeMb");
        _sort = new Picker
        {
            Title = "Sort results",
            ItemsSource = new[]
            {
                "Seeders",
                "Leechers",
                "Ratio",
                "Availability",
                "Size",
                "Published",
            },
            SelectedIndex = 0,
            TextColor = Profile.Text,
            BackgroundColor = Profile.Raised,
            MinimumHeightRequest = 48,
        };

        _searchStatus = CreateStatusLabel(
            "Configure a Torznab endpoint, enter a search term, and search.");
        _trackerStatus = CreateStatusLabel(
            "Refresh maintained public tracker lists when you need them.");
        _transferStatus = CreateStatusLabel(
            $"Transfers are saved in {_transferService.DownloadDirectory}");
        _pageTitle = new Label
        {
            Text = "Search",
            FontFamily = Profile.FontFamily,
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Profile.Text,
            VerticalOptions = LayoutOptions.Center,
        };

        _results = new CollectionView
        {
            AutomationId = "TorrentSearchResults",
            ItemsSource = _searchItems,
            SelectionMode = SelectionMode.None,
            ItemTemplate = CreateResultTemplate(),
            EmptyView = CreateStatusLabel("No results yet."),
            Margin = new Thickness(12, 0),
        };
        _trackerResults = new CollectionView
        {
            AutomationId = "TorrentTrackerResults",
            ItemsSource = _trackerItems,
            SelectionMode = SelectionMode.None,
            ItemTemplate = new DataTemplate(
                () => CreateCard(
                    new Label
                    {
                        TextColor = Profile.Text,
                        LineBreakMode = LineBreakMode.WordWrap,
                    })),
        };
        _trackerResults.ItemTemplate = new DataTemplate(
            () =>
            {
                var label = new Label
                {
                    TextColor = Profile.Text,
                    LineBreakMode = LineBreakMode.WordWrap,
                };
                label.SetBinding(Label.TextProperty, ".");
                return CreateCard(label);
            });
        _transferResults = new CollectionView
        {
            AutomationId = "TorrentTransferResults",
            ItemsSource = _transferItems,
            SelectionMode = SelectionMode.None,
            ItemTemplate = CreateTransferTemplate(),
            EmptyView = CreateStatusLabel("Add a .torrent file or download one from Search."),
            Margin = new Thickness(12, 0),
        };

        _routes[SearchRoute] = BuildSearchView();
        _routes[TransfersRoute] = BuildTransfersView();
        _routes[TrackersRoute] = BuildTrackersView();
        _routes[SettingsRoute] = BuildSettingsView();
        _transferService.Changed += TransferServiceChanged;

        var body = new Grid();
        foreach (var route in _routes.Values)
            body.Children.Add(route);

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
            Children =
            {
                GridRow(BuildHeader(), 0),
                GridRow(BuildNavigation(), 1),
                GridRow(body, 2),
            },
        };

        ShowRoute(SearchRoute);
        _ = LoadSavedApiKeyAsync();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_themeSubscribed && Application.Current is { } application)
        {
            application.RequestedThemeChanged += ThemeChanged;
            _themeSubscribed = true;
        }

        RefreshTransferItems();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_routes.TryGetValue(TransfersRoute, out var transfers)
            && transfers.IsVisible)
        {
            ShowRoute(SearchRoute);
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private View BuildHeader() =>
        new Border
        {
            Padding = new Thickness(16, 12, 16, 10),
            Background = new SolidColorBrush(Profile.Surface),
            Stroke = new SolidColorBrush(Profile.Border),
            StrokeThickness = 0,
            Content = new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    new Label
                    {
                        Text = "NOVOLIS TORRENT",
                        FontFamily = Profile.FontFamily,
                        FontSize = 12,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Profile.Accent,
                    },
                    _pageTitle,
                    new Label
                    {
                        Text = "Search, inspect, and transfer torrents from one Novolis app.",
                        FontSize = 12,
                        TextColor = Profile.Muted,
                        LineBreakMode = LineBreakMode.WordWrap,
                    },
                },
            },
        };

    private View BuildNavigation()
    {
        var navigation = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
            },
            ColumnSpacing = 4,
            Padding = new Thickness(12, 8, 12, 4),
            BackgroundColor = Profile.Background,
        };

        AddRouteButton(navigation, SearchRoute, "Search", 0);
        AddRouteButton(navigation, TransfersRoute, "Transfers", 1);
        AddRouteButton(navigation, TrackersRoute, "Trackers", 2);
        AddRouteButton(navigation, SettingsRoute, "Settings", 3);
        return navigation;
    }

    private View BuildSearchView()
    {
        var searchCard = CreateCard(
            new VerticalStackLayout
            {
                Spacing = 9,
                Children =
                {
                    SectionTitle("Indexer search"),
                    new Label
                    {
                        Text = "Torznab keeps the app provider-neutral. Use an endpoint you operate or are authorized to access.",
                        TextColor = Profile.Muted,
                        FontSize = 12,
                        LineBreakMode = LineBreakMode.WordWrap,
                    },
                    FieldRow("Endpoint", _endpoint, _apiKey),
                    FieldRow("Search", _term, ActionButton("Search", SearchAsync)),
                    BuildFilters(),
                    _searchStatus,
                },
            });

        return new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
            Children =
            {
                GridRow(searchCard, 0),
                GridRow(_results, 1),
            },
        };
    }

    private View BuildTransfersView()
    {
        var addButton = ActionButton("Add .torrent", PickTorrentAsync);
        var header = CreateCard(
            new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    SectionTitle("Transfers"),
                    new Label
                    {
                        Text = "Transfers run while Novolis Torrent is open. Files stay in app-private storage.",
                        TextColor = Profile.Muted,
                        FontSize = 12,
                        LineBreakMode = LineBreakMode.WordWrap,
                    },
                    new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Children = { addButton },
                    },
                    _transferStatus,
                },
            });

        return new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
            Children =
            {
                GridRow(header, 0),
                GridRow(_transferResults, 1),
            },
        };
    }

    private View BuildTrackersView()
    {
        var refresh = ActionButton("Refresh lists", RefreshTrackersAsync);
        var copy = ActionButton("Copy distinct URLs", CopyTrackersAsync);
        var header = CreateCard(
            new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    SectionTitle("Public tracker sources"),
                    new Label
                    {
                        Text = "The catalog combines newTrackon, ngosang/trackerslist, and XIU2/TrackersListCollection.",
                        TextColor = Profile.Muted,
                        FontSize = 12,
                        LineBreakMode = LineBreakMode.WordWrap,
                    },
                    new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Children = { refresh, copy },
                    },
                    _trackerStatus,
                },
            });

        return new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
            Children =
            {
                GridRow(header, 0),
                GridRow(_trackerResults, 1),
            },
        };
    }

    private View BuildSettingsView() =>
        new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(12, 4, 12, 16),
                Spacing = 12,
                Children =
                {
                    CreateCard(
                        new VerticalStackLayout
                        {
                            Spacing = 8,
                            Children =
                            {
                                SectionTitle("App storage"),
                                new Label
                                {
                                    Text = $"Torrent metadata: {_transferService.DownloadDirectory.Replace(
                                        "downloads",
                                        "torrents",
                                        StringComparison.OrdinalIgnoreCase)}",
                                    TextColor = Profile.Muted,
                                    FontSize = 12,
                                    LineBreakMode = LineBreakMode.WordWrap,
                                },
                                new Label
                                {
                                    Text = $"Completed and partial files: {_transferService.DownloadDirectory}",
                                    TextColor = Profile.Muted,
                                    FontSize = 12,
                                    LineBreakMode = LineBreakMode.WordWrap,
                                },
                                ActionButton(
                                    "Copy download path",
                                    CopyDownloadPathAsync),
                            },
                        }),
                    CreateCard(
                        new VerticalStackLayout
                        {
                            Spacing = 8,
                            Children =
                            {
                                SectionTitle("Network and privacy"),
                                new Label
                                {
                                    Text = "Search requests go only to the Torznab endpoint you configure. Tracker refreshes go to the three public source URLs used by the tracker library. API keys are stored with platform secure storage.",
                                    TextColor = Profile.Muted,
                                    FontSize = 12,
                                    LineBreakMode = LineBreakMode.WordWrap,
                                },
                                new Label
                                {
                                    Text = "The app does not grant shared-storage access and does not start transfers in the background.",
                                    TextColor = Profile.Muted,
                                    FontSize = 12,
                                    LineBreakMode = LineBreakMode.WordWrap,
                                },
                            },
                        }),
                },
            },
        };

    private Grid BuildFilters()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
            },
            ColumnSpacing = 8,
            RowSpacing = 8,
        };

        AddFilter(grid, "Seeders", _minimumSeeders, 0, 0);
        AddFilter(grid, "Leechers", _minimumLeechers, 1, 0);
        AddFilter(grid, "Ratio", _minimumRatio, 0, 1);
        AddFilter(grid, "Availability", _minimumAvailability, 1, 1);
        AddFilter(grid, "Minimum size (MB)", _minimumSizeMb, 0, 2);
        grid.Children.Add(_sort);
        Grid.SetColumn(_sort, 1);
        Grid.SetRow(_sort, 2);
        return grid;
    }

    private DataTemplate CreateResultTemplate() =>
        new(
            () =>
            {
                var name = new Label
                {
                    FontFamily = Profile.FontFamily,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 15,
                    TextColor = Profile.Text,
                    LineBreakMode = LineBreakMode.WordWrap,
                };
                name.SetBinding(Label.TextProperty, nameof(TorrentSearchResult.Name));

                var metadata = new Label
                {
                    FontSize = 12,
                    TextColor = Profile.Muted,
                    LineBreakMode = LineBreakMode.WordWrap,
                };
                metadata.BindingContextChanged += (_, _) =>
                {
                    if (metadata.BindingContext is TorrentSearchResult result)
                        metadata.Text = FormatResult(result);
                };

                var source = new Label
                {
                    FontSize = 12,
                    TextColor = Profile.Muted,
                    LineBreakMode = LineBreakMode.WordWrap,
                };
                source.BindingContextChanged += (_, _) =>
                {
                    if (source.BindingContext is TorrentSearchResult result)
                        source.Text = string.IsNullOrWhiteSpace(result.Source)
                            ? "Source not supplied"
                            : $"Source: {result.Source}";
                };

                Button? magnet = null;
                Button? open = null;
                Button? add = null;
                magnet = ActionButton("Copy magnet", () => CopyResultMagnetAsync(magnet!));
                open = ActionButton("Open source", () => OpenResultAsync(open!));
                add = ActionButton("Add .torrent", () => AddResultTorrentAsync(add!));

                return CreateCard(
                    new VerticalStackLayout
                    {
                        Spacing = 6,
                        Children =
                        {
                            name,
                            metadata,
                            source,
                            new HorizontalStackLayout
                            {
                                Spacing = 6,
                                Children = { magnet, add, open },
                            },
                        },
                    });
            });

    private DataTemplate CreateTransferTemplate() =>
        new(
            () =>
            {
                var name = new Label
                {
                    FontFamily = Profile.FontFamily,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 15,
                    TextColor = Profile.Text,
                    LineBreakMode = LineBreakMode.WordWrap,
                };
                name.SetBinding(Label.TextProperty, nameof(TorrentTransferItem.Name));

                var size = new Label
                {
                    FontSize = 12,
                    TextColor = Profile.Muted,
                };
                size.SetBinding(
                    Label.TextProperty,
                    new Binding(nameof(TorrentTransferItem.SizeText), stringFormat: "Size: {0}"));

                var progress = new ProgressBar
                {
                    HeightRequest = 8,
                    ProgressColor = Profile.AccentFill,
                    BackgroundColor = Profile.Raised,
                };
                progress.SetBinding(
                    ProgressBar.ProgressProperty,
                    nameof(TorrentTransferItem.ProgressFraction));

                var progressText = new Label
                {
                    FontSize = 12,
                    TextColor = Profile.Text,
                };
                progressText.SetBinding(
                    Label.TextProperty,
                    new Binding(nameof(TorrentTransferItem.ProgressText), stringFormat: "{0}"));

                var metrics = new Label
                {
                    FontSize = 12,
                    TextColor = Profile.Muted,
                    LineBreakMode = LineBreakMode.WordWrap,
                };
                metrics.SetBinding(Label.TextProperty, nameof(TorrentTransferItem.MetricsText));

                var status = new Label
                {
                    FontSize = 12,
                    TextColor = Profile.Muted,
                };
                status.SetBinding(
                    Label.TextProperty,
                    new Binding(nameof(TorrentTransferItem.Status), stringFormat: "Status: {0}"));

                Button? start = null;
                Button? stop = null;
                start = ActionButton("Start", () => StartTransferAsync(start!));
                stop = ActionButton("Stop", () => StopTransferAsync(stop!));
                return CreateCard(
                    new VerticalStackLayout
                    {
                        Spacing = 6,
                        Children =
                        {
                            name,
                            size,
                            progress,
                            progressText,
                            metrics,
                            status,
                            new HorizontalStackLayout
                            {
                                Spacing = 6,
                                Children = { start, stop },
                            },
                        },
                    });
            });

    private async Task SearchAsync()
    {
        if (_busy)
            return;

        if (!Uri.TryCreate(
                _endpoint.Text?.Trim(),
                UriKind.Absolute,
                out var endpoint)
            || endpoint.Scheme is not ("http" or "https"))
        {
            SetStatus("Enter an absolute HTTP or HTTPS Torznab endpoint.");
            return;
        }

        if (!TryReadInt(_minimumSeeders, "seeders", out var seeders)
            || !TryReadInt(_minimumLeechers, "leechers", out var leechers)
            || !TryReadDecimal(_minimumRatio, "ratio", out var ratio)
            || !TryReadDecimal(
                _minimumAvailability,
                "availability",
                out var availability)
            || !TryReadSize(out var size))
        {
            return;
        }

        try
        {
            var query = new TorrentSearchQuery(_term.Text ?? string.Empty)
            {
                MinimumSeeders = seeders,
                MinimumLeechers = leechers,
                MinimumRatio = ratio,
                MinimumAvailability = availability,
                MinimumSizeBytes = size,
                SortBy = GetSort(),
                Limit = 100,
            };
            query.Validate();
            await SaveSettingsAsync();
            _busy = true;
            SetStatus("Searching…");
            var provider = new TorznabSearchProvider(
                _httpClient,
                endpoint,
                _apiKey.Text);
            var client = new TorrentSearchClient(provider);
            var results = await client.SearchAsync(query);
            _searchItems.Clear();
            foreach (var result in results)
                _searchItems.Add(result);
            SetStatus(
                results.Count == 0
                    ? "No results matched the active filters."
                    : $"Found {results.Count:N0} result(s) from {provider.Name}.");
        }
        catch (Exception exception)
        {
            SetStatus($"Search failed: {exception.Message}");
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task RefreshTrackersAsync()
    {
        if (_busy)
            return;

        _busy = true;
        _trackerItems.Clear();
        var trackers = new List<Uri>();
        var entries = new List<string>();
        try
        {
            SetTrackerStatus("Refreshing newTrackon, ngosang, and XIU2…");
            foreach (var source in _trackerCatalog.Sources)
            {
                try
                {
                    var list = await source.GetAsync(TorrentTrackerListKind.Best);
                    trackers.AddRange(list.Trackers);
                    entries.Add($"{list.Source}: {list.Trackers.Count:N0} trackers");
                }
                catch (Exception exception)
                {
                    entries.Add($"{source.Kind}: unavailable · {exception.Message}");
                }
            }

            _trackerUris = trackers
                .GroupBy(uri => uri.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();
            foreach (var entry in entries)
                _trackerItems.Add(entry);
            SetTrackerStatus(
                $"Loaded {_trackerUris.Count:N0} distinct tracker URL(s) across {entries.Count:N0} sources.");
        }
        catch (Exception exception)
        {
            SetTrackerStatus($"Tracker refresh failed: {exception.Message}");
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task PickTorrentAsync()
    {
        if (_busy)
            return;

        try
        {
            var file = await FilePicker.Default.PickAsync(
                new PickOptions
                {
                    PickerTitle = "Select a .torrent file",
                });
            if (file is null)
                return;

            _busy = true;
            SetTransferStatus("Importing torrent metadata…");
            await _transferService.AddFileAsync(file);
            RefreshTransferItems();
            ShowRoute(TransfersRoute);
            SetTransferStatus("Torrent added. Press Start when you are ready.");
        }
        catch (Exception exception)
        {
            SetTransferStatus($"Could not add torrent: {exception.Message}");
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task AddResultTorrentAsync(Button sender)
    {
        if (sender.BindingContext is not TorrentSearchResult result
            || result.TorrentUri is null)
        {
            SetStatus("This result has no downloadable .torrent URL.");
            return;
        }

        try
        {
            _busy = true;
            SetStatus("Downloading torrent metadata…");
            await _transferService.AddUriAsync(result.TorrentUri);
            RefreshTransferItems();
            ShowRoute(TransfersRoute);
            SetTransferStatus("Torrent added. Press Start when you are ready.");
        }
        catch (Exception exception)
        {
            SetStatus($"Could not add torrent: {exception.Message}");
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task CopyResultMagnetAsync(Button sender)
    {
        if (sender.BindingContext is not TorrentSearchResult result
            || result.MagnetUri is null)
        {
            SetStatus("This result has no magnet URI.");
            return;
        }

        await Clipboard.Default.SetTextAsync(result.MagnetUri.AbsoluteUri);
        SetStatus("Magnet URI copied.");
    }

    private async Task OpenResultAsync(Button sender)
    {
        if (sender.BindingContext is not TorrentSearchResult result)
        {
            SetStatus("No result is selected.");
            return;
        }

        var uri = result.TorrentUri ?? result.MagnetUri;
        if (uri is null)
        {
            SetStatus("This result has no source URI.");
            return;
        }

        try
        {
            await Launcher.Default.OpenAsync(uri);
        }
        catch (Exception exception)
        {
            SetStatus($"Could not open source: {exception.Message}");
        }
    }

    private async Task StartTransferAsync(Button sender)
    {
        if (sender.BindingContext is not TorrentTransferItem item)
            return;

        try
        {
            SetTransferStatus($"Starting {item.Name}…");
            await _transferService.StartAsync(item);
            SetTransferStatus($"Started {item.Name}.");
        }
        catch (Exception exception)
        {
            SetTransferStatus($"Could not start transfer: {exception.Message}");
        }
    }

    private async Task StopTransferAsync(Button sender)
    {
        if (sender.BindingContext is not TorrentTransferItem item)
            return;

        try
        {
            await _transferService.StopAsync(item);
            SetTransferStatus($"Stopped {item.Name}.");
        }
        catch (Exception exception)
        {
            SetTransferStatus($"Could not stop transfer: {exception.Message}");
        }
    }

    private async Task CopyTrackersAsync()
    {
        if (_trackerUris.Count == 0)
        {
            SetTrackerStatus("Refresh the tracker lists before copying URLs.");
            return;
        }

        await Clipboard.Default.SetTextAsync(
            string.Join(Environment.NewLine, _trackerUris.Select(uri => uri.AbsoluteUri)));
        SetTrackerStatus($"Copied {_trackerUris.Count:N0} distinct tracker URL(s).");
    }

    private async Task CopyDownloadPathAsync()
    {
        await Clipboard.Default.SetTextAsync(_transferService.DownloadDirectory);
        SetStatus("Download path copied.");
    }

    private async Task LoadSavedApiKeyAsync()
    {
        try
        {
            var value = await SecureStorage.Default.GetAsync("torrent.torznab.api-key");
            if (!string.IsNullOrWhiteSpace(value))
                await MainThread.InvokeOnMainThreadAsync(() => _apiKey.Text = value);
        }
        catch
        {
            // A platform may not provide secure storage; an empty key remains valid.
        }
    }

    private async Task SaveSettingsAsync()
    {
        Preferences.Default.Set("torrent.endpoint", _endpoint.Text?.Trim() ?? string.Empty);
        try
        {
            await SecureStorage.Default.SetAsync(
                "torrent.torznab.api-key",
                _apiKey.Text?.Trim() ?? string.Empty);
        }
        catch
        {
            // Search still works with providers that do not require an API key.
        }
    }

    private void TransferServiceChanged(object? sender, EventArgs args) =>
        MainThread.BeginInvokeOnMainThread(RefreshTransferItems);

    private void RefreshTransferItems()
    {
        _transferItems.Clear();
        foreach (var item in _transferService.Items)
            _transferItems.Add(item);
    }

    private void ThemeChanged(object? sender, AppThemeChangedEventArgs args)
    {
        BackgroundColor = Profile.Background;
        _pageTitle.TextColor = Profile.Text;
    }

    private void AddRouteButton(Grid grid, string route, string label, int column)
    {
        var button = new Button
        {
            Text = label,
            FontFamily = Profile.FontFamily,
            FontAttributes = FontAttributes.Bold,
            FontSize = 12,
            TextColor = Profile.Text,
            BackgroundColor = Profile.Raised,
            BorderColor = Profile.Border,
            BorderWidth = 1,
            CornerRadius = 8,
            MinimumHeightRequest = 48,
        };
        button.Clicked += (_, _) => ShowRoute(route);
        _routeButtons[route] = button;
        grid.Children.Add(button);
        Grid.SetColumn(button, column);
    }

    private void ShowRoute(string route)
    {
        foreach (var pair in _routes)
            pair.Value.IsVisible = pair.Key == route;
        foreach (var pair in _routeButtons)
            pair.Value.BackgroundColor = pair.Key == route
                ? Profile.ActionSoft
                : Profile.Raised;
        _pageTitle.Text = route switch
        {
            TransfersRoute => "Transfers",
            TrackersRoute => "Trackers",
            SettingsRoute => "Settings",
            _ => "Search",
        };
    }

    private TorrentSearchSort GetSort() =>
        (_sort.SelectedItem as string) switch
        {
            "Leechers" => TorrentSearchSort.Leechers,
            "Ratio" => TorrentSearchSort.Ratio,
            "Availability" => TorrentSearchSort.Availability,
            "Size" => TorrentSearchSort.Size,
            "Published" => TorrentSearchSort.PublishedAt,
            _ => TorrentSearchSort.Seeders,
        };

    private bool TryReadInt(Entry entry, string label, out int? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(entry.Text))
            return true;
        if (int.TryParse(
                entry.Text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed)
            && parsed >= 0)
        {
            value = parsed;
            return true;
        }

        SetStatus($"The {label} filter must be a non-negative integer.");
        return false;
    }

    private bool TryReadDecimal(Entry entry, string label, out decimal? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(entry.Text))
            return true;
        if (decimal.TryParse(
                entry.Text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed)
            && parsed >= 0)
        {
            value = parsed;
            return true;
        }

        SetStatus($"The {label} filter must be a non-negative number.");
        return false;
    }

    private bool TryReadSize(out long? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(_minimumSizeMb.Text))
            return true;
        if (decimal.TryParse(
                _minimumSizeMb.Text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed)
            && parsed >= 0
            && parsed <= long.MaxValue / (1024m * 1024m))
        {
            value = (long)(parsed * 1024m * 1024m);
            return true;
        }

        SetStatus("The minimum size must be a non-negative number of megabytes.");
        return false;
    }

    private void SetStatus(string text) => _searchStatus.Text = text;

    private void SetTrackerStatus(string text) => _trackerStatus.Text = text;

    private void SetTransferStatus(string text) => _transferStatus.Text = text;

    private static string FormatResult(TorrentSearchResult result)
    {
        var parts = new List<string>
        {
            result.SizeBytes is { } size ? FormatBytes(size) : "size unknown",
            result.Seeders is { } seeders ? $"↑ {seeders:N0}" : "↑ —",
            result.Leechers is { } leechers ? $"↓ {leechers:N0}" : "↓ —",
            result.EffectiveRatio is { } ratio
                ? $"ratio {FormatRatio(ratio)}"
                : "ratio —",
            result.Availability is { } availability
                ? $"availability {availability:0.##}"
                : "availability —",
        };
        if (!string.IsNullOrWhiteSpace(result.Category))
            parts.Add(result.Category);
        return string.Join("  ·  ", parts);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes:N0} B";
        var value = (double)bytes;
        var units = new[] { "KB", "MB", "GB", "TB" };
        var index = -1;
        do
        {
            value /= 1024;
            index++;
        }
        while (value >= 1024 && index < units.Length - 1);
        return $"{value:0.##} {units[index]}";
    }

    private static string FormatRatio(decimal ratio) =>
        ratio == decimal.MaxValue ? "∞" : ratio.ToString("0.##", CultureInfo.InvariantCulture);

    private static Entry CreateEntry(
        string placeholder,
        string text,
        string automationId) =>
        new()
        {
            AutomationId = automationId,
            Placeholder = placeholder,
            Text = text,
            TextColor = Profile.Text,
            PlaceholderColor = Profile.Muted,
            BackgroundColor = Profile.Raised,
            ClearButtonVisibility = ClearButtonVisibility.WhileEditing,
            MinimumHeightRequest = 48,
        };

    private static Label CreateStatusLabel(string text) =>
        new()
        {
            Text = text,
            TextColor = Profile.Muted,
            FontSize = 12,
            LineBreakMode = LineBreakMode.WordWrap,
        };

    private static Label SectionTitle(string text) =>
        new()
        {
            Text = text,
            FontFamily = Profile.FontFamily,
            FontAttributes = FontAttributes.Bold,
            FontSize = 18,
            TextColor = Profile.Text,
        };

    private static Grid FieldRow(string label, View primary, View secondary)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(96)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(210)),
            },
            ColumnSpacing = 8,
        };
        grid.Children.Add(
            new Label
            {
                Text = label,
                TextColor = Profile.Muted,
                VerticalTextAlignment = TextAlignment.Center,
            });
        grid.Children.Add(primary);
        grid.Children.Add(secondary);
        Grid.SetColumn(primary, 1);
        Grid.SetColumn(secondary, 2);
        return grid;
    }

    private static void AddFilter(
        Grid grid,
        string label,
        Entry entry,
        int column,
        int row)
    {
        var field = new VerticalStackLayout
        {
            Spacing = 3,
            Children =
            {
                new Label
                {
                    Text = label,
                    TextColor = Profile.Muted,
                    FontSize = 11,
                },
                entry,
            },
        };
        grid.Children.Add(field);
        Grid.SetColumn(field, column);
        Grid.SetRow(field, row);
    }

    private Button ActionButton(string text, Func<Task> action)
    {
        var button = new Button
        {
            Text = text,
            FontFamily = Profile.FontFamily,
            FontAttributes = FontAttributes.Bold,
            FontSize = 12,
            TextColor = Profile.OnAction,
            BackgroundColor = Profile.Action,
            BorderColor = Profile.Border,
            BorderWidth = 1,
            CornerRadius = 8,
            MinimumHeightRequest = 46,
            Padding = new Thickness(12, 6),
        };
        SemanticProperties.SetDescription(button, text);
        button.Clicked += async (_, _) =>
        {
            try
            {
                await action();
            }
            catch (Exception exception)
            {
                SetStatus($"Action failed: {exception.Message}");
            }
        };
        return button;
    }

    private static Border CreateCard(View content) =>
        new()
        {
            Margin = new Thickness(12, 8, 12, 0),
            Padding = new Thickness(14),
            Background = new SolidColorBrush(Profile.Surface),
            Stroke = new SolidColorBrush(Profile.Border),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle
            {
                CornerRadius = new CornerRadius(10),
            },
            Content = content,
        };

    private static View GridRow(View view, int row)
    {
        Grid.SetRow(view, row);
        return view;
    }
}
