using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Shapes;
using Novolis.Commands.Navigation;
using Novolis.Maui.GraphicalProfile;
using Novolis.Maui.Navigation;
using Novolis.NMule.Core;
using Profile = Novolis.Maui.GraphicalProfile.GraphicalProfile;

namespace Novolis.NMule;

/// <summary>Foreground nMule search, Kad, and download surface.</summary>
public sealed class MainPage : ContentPage
{
    private const double ResultGridWidth = 1_820;
    private const int ResultGridColumnCount = 9;
    private const string AllTypesFilter = "All types";
    private const string AllAvailabilityFilter = "All availability";
    private const string AllSpeedFilter = "All advertised speeds";
    private const string AllTransferPathsFilter = "All transfer paths";
    private const string OriginalOrderSort = "Original order";
    private readonly NmuleSession _session;
    private readonly MobileNavigationShell _navigationShell;
    private readonly Entry _serverSource;
    private readonly Entry _kadSource;
    private readonly Entry _query;
    private readonly Entry _resultFilter;
    private readonly Label _actionStatus;
    private readonly Picker _resultTypeFilter;
    private readonly Picker _resultAvailabilityFilter;
    private readonly Picker _resultSpeedFilter;
    private readonly Picker _resultTransferFilter;
    private readonly Picker _resultSort;
    private readonly Label _status;
    private readonly Label _networkStatus;
    private readonly Label _searchNetworkStatus;
    private readonly Label _serverStatus;
    private readonly Label _kadStatus;
    private readonly Label _downloadStatus;
    private readonly Label _resultStatus;
    private readonly Label _outputLocation;
    private readonly CollectionView _serverList;
    private readonly CollectionView _results;
    private readonly CollectionView _downloads;
    private readonly Switch _allowTailscale;
    private readonly Switch _allowUnrestrictedNetwork;
    private readonly ObservableCollection<NmuleSearchHit> _searchResults = [];
    private readonly ObservableCollection<NmuleServerRow> _serverRows = [];
    private readonly ObservableCollection<NmuleDownloadResult> _downloadResults = [];
    private readonly View _searchView;
    private readonly View _downloadsView;
    private readonly View _settingsView;
    private IReadOnlyList<NmuleSearchHit> _allSearchResults = [];
    private bool _busy;
    private bool _themeSubscribed;
    private NmuleServerRow? _selectedServer;

    /// <summary>Creates the nMule page.</summary>
    public MainPage(NmuleSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _session.StatusChanged += OnSessionStatusChanged;

        Title = "nMule";
        _status = new Label
        {
            AutomationId = "NmuleStatus",
            Text = "Ready. nMule requires an active VPN.",
            LineBreakMode = LineBreakMode.WordWrap,
        };
        _actionStatus = new Label
        {
            AutomationId = "NmuleActionStatus",
            Text = "Ready.",
            FontSize = GraphicalProfileColors.CaptionSize,
            TextColor = Profile.Muted,
            LineBreakMode = LineBreakMode.WordWrap,
        };
        _networkStatus = new Label
        {
            AutomationId = "NmuleNetworkStatus",
            Text = "Checking VPN policy...",
            LineBreakMode = LineBreakMode.WordWrap,
        };
        _searchNetworkStatus = new Label
        {
            AutomationId = "NmuleSearchNetworkStatus",
            Text = "Checking VPN policy...",
            LineBreakMode = LineBreakMode.WordWrap,
        };
        _serverStatus = new Label
        {
            AutomationId = "NmuleServerStatus",
            Text = "No eD2K server list loaded.",
            LineBreakMode = LineBreakMode.WordWrap,
        };
        _kadStatus = new Label
        {
            AutomationId = "NmuleKadStatus",
            Text = "Kad is not bootstrapped.",
            LineBreakMode = LineBreakMode.WordWrap,
        };
        _downloadStatus = new Label
        {
            AutomationId = "NmuleDownloadStatus",
            Text = "No downloads completed in this session.",
            LineBreakMode = LineBreakMode.WordWrap,
        };

        _serverSource = CreateEntry(
            "HTTPS server.met source (clear to disable)",
            NmuleDefaults.ServerListUrl,
            "NmuleServerSource");
        _kadSource = CreateEntry(
            "HTTPS nodes.dat source (clear to disable)",
            NmuleDefaults.KadNodesUrl,
            "NmuleKadSource");
        _query = CreateEntry(
            "Search terms",
            string.Empty,
            "NmuleSearchQuery");
        _query.ReturnType = ReturnType.Search;
        _query.Completed += (_, _) => _ = SearchAsync();

        _resultFilter = CreateEntry(
            "Filter by name, type, or file ID",
            string.Empty,
            "NmuleResultFilter");
        _resultFilter.TextChanged += (_, _) => ApplyResultFilters();
        _resultTypeFilter = CreatePicker(
            AllTypesFilter,
            "NmuleResultTypeFilter",
            [AllTypesFilter]);
        _resultAvailabilityFilter = CreatePicker(
            AllAvailabilityFilter,
            "NmuleResultAvailabilityFilter",
            [AllAvailabilityFilter, "Has advertised sources", "No advertised sources"]);
        _resultSpeedFilter = CreatePicker(
            AllSpeedFilter,
            "NmuleResultSpeedFilter",
            [AllSpeedFilter, "Speed advertised", "Speed not advertised"]);
        _resultTransferFilter = CreatePicker(
            AllTransferPathsFilter,
            "NmuleResultTransferFilter",
            [
                AllTransferPathsFilter,
                "Direct download",
                "Kad source lookup",
                "Unavailable",
            ]);
        _resultSort = CreatePicker(
            OriginalOrderSort,
            "NmuleResultSort",
            [
                OriginalOrderSort,
                "Name A-Z",
                "Name Z-A",
                "Smallest first",
                "Largest first",
                "Most sources",
                "Fewest sources",
                "Fastest advertised",
                "Slowest advertised",
            ]);
        _resultTypeFilter.SelectedIndexChanged += (_, _) => ApplyResultFilters();
        _resultAvailabilityFilter.SelectedIndexChanged += (_, _) =>
            ApplyResultFilters();
        _resultSpeedFilter.SelectedIndexChanged += (_, _) => ApplyResultFilters();
        _resultTransferFilter.SelectedIndexChanged += (_, _) =>
            ApplyResultFilters();
        _resultSort.SelectedIndexChanged += (_, _) => ApplyResultFilters();
        _resultStatus = new Label
        {
            AutomationId = "NmuleResultStatus",
            Text = "No search results yet.",
            FontSize = GraphicalProfileColors.CaptionSize,
            TextColor = Profile.Muted,
        };
        _outputLocation = new Label
        {
            AutomationId = "NmuleOutputLocation",
            Text = $"Output: {_session.OutputDirectory}",
            FontSize = GraphicalProfileColors.CaptionSize,
            TextColor = Profile.Muted,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1,
        };

        _allowTailscale = new Switch
        {
            AutomationId = "NmuleAllowTailscale",
            IsVisible = _session.NetworkGate.SupportsTailscaleOverride,
            IsToggled = _session.NetworkGate.AllowTailscale,
            IsEnabled = !_session.NetworkGate.AllowUnrestrictedNetwork,
            HorizontalOptions = LayoutOptions.End,
        };
        SemanticProperties.SetDescription(
            _allowTailscale,
            "Allow Tailscale for this nMule session");
        _allowTailscale.Toggled += (_, args) =>
        {
            _session.NetworkGate.AllowTailscale = args.Value;
            _ = RefreshNetworkStatusAsync();
        };

        _allowUnrestrictedNetwork = new Switch
        {
            AutomationId = "NmuleAllowUnrestrictedNetwork",
            IsVisible = _session.NetworkGate.SupportsUnrestrictedNetworkOverride,
            IsToggled = _session.NetworkGate.AllowUnrestrictedNetwork,
            HorizontalOptions = LayoutOptions.End,
        };
        SemanticProperties.SetDescription(
            _allowUnrestrictedNetwork,
            "Allow nMule network access without a VPN for this session");
        _allowUnrestrictedNetwork.Toggled += (_, args) =>
        {
            _session.NetworkGate.AllowUnrestrictedNetwork = args.Value;
            _allowTailscale.IsEnabled = !args.Value;
            _ = RefreshNetworkStatusAsync();
        };

        _results = new CollectionView
        {
            AutomationId = "NmuleSearchResults",
            ItemsSource = _searchResults,
            SelectionMode = SelectionMode.None,
            WidthRequest = ResultGridWidth,
            HeightRequest = 460,
            ItemsLayout = new LinearItemsLayout(
                ItemsLayoutOrientation.Vertical)
            {
                ItemSpacing = 1,
            },
            ItemTemplate = CreateSearchTemplate(),
        };
        _downloads = new CollectionView
        {
            AutomationId = "NmuleDownloads",
            ItemsSource = _downloadResults,
            SelectionMode = SelectionMode.None,
            ItemTemplate = CreateDownloadTemplate(),
        };
        _serverList = new CollectionView
        {
            AutomationId = "NmuleServerList",
            ItemsSource = _serverRows,
            SelectionMode = SelectionMode.Single,
            IsVisible = false,
            HeightRequest = 260,
            ItemTemplate = CreateServerTemplate(),
        };
        _serverList.SelectionChanged += OnServerSelectionChanged;

        _searchView = BuildSearchView();
        _downloadsView = BuildDownloadsView();
        _settingsView = BuildSettingsView();
        var body = new Grid
        {
            Children =
            {
                _searchView,
                _downloadsView,
                _settingsView,
            },
        };

        var menuTree = new MobileMenuTree(
        [
            new MobileMenuNode(
                "nmule.search",
                "Search",
                MobileMenuNodeKind.Destination)
            {
                Placement = MobileMenuPlacement.Primary,
                RouteId = "search",
            },
            new MobileMenuNode(
                "nmule.downloads",
                "Downloads",
                MobileMenuNodeKind.Destination)
            {
                Placement = MobileMenuPlacement.Primary,
                RouteId = "downloads",
            },
            new MobileMenuNode(
                "nmule.settings",
                "Settings",
                MobileMenuNodeKind.Destination)
            {
                RouteId = "settings",
            },
        ]);
        _navigationShell = new MobileNavigationShell(
            new MobileNavigationController(menuTree))
        {
            Title = "nMule · Search",
            Body = body,
        };
        _navigationShell.NavigationChanged += OnNavigationChanged;
        Content = _navigationShell;
        ApplyRoute("search");
        _ = RefreshNetworkStatusAsync();
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

        _ = RefreshNetworkStatusAsync();
    }

    /// <inheritdoc />
    protected override bool OnBackButtonPressed() =>
        _navigationShell.TryHandleBack() || base.OnBackButtonPressed();

    private View BuildSearchView()
    {
        var serverCard = CreateCard(
            new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    CreateSectionTitle("eD2K server"),
                    _status,
                    _searchNetworkStatus,
                    _serverSource,
                    new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Children =
                        {
                            CreateButton("Load servers", "NmuleLoadServers", LoadServersAsync),
                            CreateButton("Connect", "NmuleConnectServer", ConnectServerAsync),
                        },
                    },
                    _serverStatus,
                    _serverList,
                },
            });

        var kadCard = CreateCard(
            new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    CreateSectionTitle("Kad"),
                    _kadSource,
                    CreateButton("Bootstrap Kad", "NmuleBootstrapKad", BootstrapKadAsync),
                    _kadStatus,
                },
            });

        var searchCard = CreateCard(
            new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    CreateSectionTitle("Search"),
                    _query,
                    CreateButton("Search", "NmuleSearch", SearchAsync),
                    _actionStatus,
                    CreateSectionTitle("Results"),
                    _outputLocation,
                    _resultFilter,
                    new Grid
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
                        Children =
                        {
                            _resultTypeFilter,
                            _resultAvailabilityFilter,
                            _resultSpeedFilter,
                            _resultTransferFilter,
                            _resultSort,
                        },
                    },
                    _resultStatus,
                    CreateResultsGrid(),
                },
            });
        Grid.SetColumn(_resultTypeFilter, 0);
        Grid.SetRow(_resultTypeFilter, 0);
        Grid.SetColumn(_resultAvailabilityFilter, 1);
        Grid.SetRow(_resultAvailabilityFilter, 0);
        Grid.SetColumn(_resultSpeedFilter, 0);
        Grid.SetRow(_resultSpeedFilter, 1);
        Grid.SetColumn(_resultTransferFilter, 1);
        Grid.SetRow(_resultTransferFilter, 1);
        Grid.SetColumn(_resultSort, 0);
        Grid.SetRow(_resultSort, 2);
        Grid.SetColumnSpan(_resultSort, 2);

        return new ScrollView
        {
            AutomationId = "NmuleSearchSurface",
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(12, 4, 12, 16),
                Spacing = 12,
                Children =
                {
                    serverCard,
                    kadCard,
                    searchCard,
                },
            },
        };
    }

    private View BuildDownloadsView() =>
        new ScrollView
        {
            AutomationId = "NmuleDownloadsSurface",
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
                                CreateSectionTitle("Completed downloads"),
                                new Label
                                {
                                    Text = "The MVP keeps completed files in nMule's app-private storage.",
                                    LineBreakMode = LineBreakMode.WordWrap,
                                },
                                _downloadStatus,
                                _downloads,
                            },
                        }),
                },
            },
        };

    private View BuildNetworkPolicyCard()
    {
        var controls = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 12,
        };
        var tailscaleLabel = new Label
        {
            Text = "Allow Tailscale for this session",
            VerticalTextAlignment = TextAlignment.Center,
        };
        var unrestrictedLabel = new Label
        {
            Text = "Allow network without VPN (not recommended)",
            VerticalTextAlignment = TextAlignment.Center,
        };
        controls.Children.Add(tailscaleLabel);
        controls.Children.Add(_allowTailscale);
        controls.Children.Add(unrestrictedLabel);
        controls.Children.Add(_allowUnrestrictedNetwork);
        Grid.SetColumn(_allowTailscale, 1);
        Grid.SetRow(_allowTailscale, 0);
        Grid.SetRow(unrestrictedLabel, 1);
        Grid.SetColumn(_allowUnrestrictedNetwork, 1);
        Grid.SetRow(_allowUnrestrictedNetwork, 1);

        var card = CreateCard(
            new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    CreateSectionTitle("Network policy"),
                    _networkStatus,
                    controls,
                    new Label
                    {
                        Text = "VPN access is checked before DNS, HTTPS, TCP, and UDP activity. Both overrides are session-only and reset when nMule restarts.",
                        FontSize = GraphicalProfileColors.CaptionSize,
                        TextColor = Profile.Muted,
                        LineBreakMode = LineBreakMode.WordWrap,
                    },
                },
            });
        return card;
    }

    private View BuildSettingsView() =>
        new ScrollView
        {
            AutomationId = "NmuleSettingsSurface",
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(12, 4, 12, 16),
                Spacing = 12,
                Children =
                {
                    BuildNetworkPolicyCard(),
                    CreateCard(
                        new VerticalStackLayout
                        {
                            Spacing = 8,
                            Children =
                            {
                                CreateSectionTitle("About nMule"),
                                new Label
                                {
                                    Text = "nMule is a foreground Android and Windows eD2K/Kad client. It uses the clean-room Novolis transport packages and does not start background transfers.",
                                    LineBreakMode = LineBreakMode.WordWrap,
                                },
                                new Label
                                {
                                    Text = "Transfers are verified across all eD2K parts and written to the app-private output location. LowID callback hosting, uploads, and resume scheduling are intentionally unavailable in this MVP.",
                                    FontSize = GraphicalProfileColors.CaptionSize,
                                    TextColor = Profile.Muted,
                                    LineBreakMode = LineBreakMode.WordWrap,
                                },
                            },
                        }),
                },
            },
        };

    private View CreateResultsGrid() =>
        new ScrollView
        {
            AutomationId = "NmuleResultsGrid",
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Always,
            Content = new VerticalStackLayout
            {
                Spacing = 1,
                WidthRequest = ResultGridWidth,
                Children =
                {
                    CreateResultsGridHeader(),
                    _results,
                },
            },
        };

    private static Grid CreateResultsGridHeader()
    {
        var grid = CreateResultGrid();
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        AddResultCell(
            grid,
            CreateGridHeaderLabel("Name"),
            0);
        AddResultCell(
            grid,
            CreateGridHeaderLabel("Size"),
            1);
        AddResultCell(
            grid,
            CreateGridHeaderLabel("Availability"),
            2);
        AddResultCell(
            grid,
            CreateGridHeaderLabel("Speed"),
            3);
        AddResultCell(
            grid,
            CreateGridHeaderLabel("Hash"),
            4);
        AddResultCell(
            grid,
            CreateGridHeaderLabel("Same hash / names"),
            5);
        AddResultCell(
            grid,
            CreateGridHeaderLabel("Transfer"),
            6);
        AddResultCell(
            grid,
            CreateGridHeaderLabel("Output location"),
            7);
        AddResultCell(
            grid,
            CreateGridHeaderLabel("Actions"),
            8);
        return grid;
    }

    private DataTemplate CreateSearchTemplate() =>
        new(() =>
        {
            var name = CreateGridValueLabel(bold: true);
            name.SetBinding(Label.TextProperty, nameof(NmuleSearchHit.Name));
            var size = CreateGridValueLabel();
            size.SetBinding(
                Label.TextProperty,
                new Binding(
                    nameof(NmuleSearchHit.Size),
                    stringFormat: "{0:N0} bytes"));
            var availability = CreateGridValueLabel();
            availability.SetBinding(
                Label.TextProperty,
                nameof(NmuleSearchHit.AvailabilityLabel));
            var speed = CreateGridValueLabel();
            speed.SetBinding(Label.TextProperty, nameof(NmuleSearchHit.SpeedLabel));
            var hash = CreateGridValueLabel();
            hash.SetBinding(Label.TextProperty, nameof(NmuleSearchHit.HashLabel));
            var names = CreateGridValueLabel();
            names.SetBinding(
                Label.TextProperty,
                nameof(NmuleSearchHit.SameHashNamesLabel));
            var transfer = CreateGridValueLabel();
            transfer.SetBinding(
                Label.TextProperty,
                nameof(NmuleSearchHit.EligibilityLabel));
            var output = CreateGridValueLabel();
            output.SetBinding(
                Label.TextProperty,
                nameof(NmuleSearchHit.OutputLocationLabel));
            var metadata = CreateGridValueLabel();
            metadata.SetBinding(
                Label.TextProperty,
                nameof(NmuleSearchHit.DetailsText));
            metadata.IsVisible = false;

            Button? download = null;
            download = CreateButton(
                "Download",
                "NmuleDownloadResult",
                () => DownloadFromButtonAsync(download));
            Button? toggleDetails = null;
            toggleDetails = CreateButton(
                "More details",
                "NmuleResultDetails",
                () =>
                {
                    metadata.IsVisible = !metadata.IsVisible;
                    if (toggleDetails is not null)
                    {
                        toggleDetails.Text = metadata.IsVisible
                            ? "Hide details"
                            : "More details";
                    }

                    return Task.CompletedTask;
                });
            var actions = new VerticalStackLayout
            {
                Spacing = 4,
                Children = { download, toggleDetails },
            };

            var row = CreateResultGrid();
            row.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            row.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AddResultCell(row, name, 0);
            AddResultCell(row, size, 1);
            AddResultCell(row, availability, 2);
            AddResultCell(row, speed, 3);
            AddResultCell(row, hash, 4);
            AddResultCell(row, names, 5);
            AddResultCell(row, transfer, 6);
            AddResultCell(row, output, 7);
            AddResultCell(row, actions, 8);
            AddResultCell(row, metadata, 0, 1, ResultGridColumnCount);
            return row;
        });

    private static Grid CreateResultGrid()
    {
        var grid = new Grid
        {
            WidthRequest = ResultGridWidth,
            ColumnSpacing = 1,
            RowSpacing = 1,
            BackgroundColor = Profile.Border,
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(220)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(120)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(190)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(140)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(280)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(300)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(160)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(260)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(140)));
        return grid;
    }

    private static void AddResultCell(
        Grid grid,
        View content,
        int column,
        int row = 0,
        int columnSpan = 1)
    {
        var cell = new Grid
        {
            Padding = new Thickness(8, 7),
            BackgroundColor = Profile.Surface,
            MinimumHeightRequest = GraphicalProfileColors.TouchTarget,
            Children = { content },
        };
        grid.Children.Add(cell);
        Grid.SetColumn(cell, column);
        Grid.SetRow(cell, row);
        if (columnSpan > 1)
            Grid.SetColumnSpan(cell, columnSpan);
    }

    private static Label CreateGridHeaderLabel(string text) =>
        new()
        {
            Text = text,
            FontFamily = Profile.FontFamily,
            FontAttributes = FontAttributes.Bold,
            TextColor = Profile.Text,
            LineBreakMode = LineBreakMode.WordWrap,
        };

    private static Label CreateGridValueLabel(bool bold = false) =>
        new()
        {
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
            FontSize = GraphicalProfileColors.CaptionSize,
            TextColor = Profile.Text,
            LineBreakMode = LineBreakMode.WordWrap,
            MaxLines = 4,
            VerticalTextAlignment = TextAlignment.Center,
        };

    private DataTemplate CreateServerTemplate() =>
        new(() =>
        {
            var server = new Label
            {
                FontSize = GraphicalProfileColors.CaptionSize,
                TextColor = Profile.Text,
                LineBreakMode = LineBreakMode.WordWrap,
            };
            server.SetBinding(Label.TextProperty, nameof(NmuleServerRow.Summary));
            return CreateCard(server);
        });

    private DataTemplate CreateDownloadTemplate() =>
        new(() =>
        {
            var name = new Label
            {
                FontAttributes = FontAttributes.Bold,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1,
            };
            name.SetBinding(Label.TextProperty, nameof(NmuleDownloadResult.FileName));
            var details = new Label
            {
                FontSize = GraphicalProfileColors.CaptionSize,
                TextColor = Profile.Muted,
            };
            details.SetBinding(
                Label.TextProperty,
                new Binding(
                    nameof(NmuleDownloadResult.Size),
                    stringFormat: "{0:N0} bytes"));
            var source = new Label
            {
                                    FontSize = GraphicalProfileColors.CaptionSize,
                TextColor = Profile.Muted,
            };
            source.SetBinding(
                Label.TextProperty,
                nameof(NmuleDownloadResult.SourceDescription));
            var path = new Label
            {
                FontSize = GraphicalProfileColors.CaptionSize,
                TextColor = Profile.Muted,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 2,
            };
            path.SetBinding(
                Label.TextProperty,
                new Binding(
                    nameof(NmuleDownloadResult.Path),
                    stringFormat: "Output: {0}"));
            return CreateCard(
                new VerticalStackLayout
                {
                    Spacing = 4,
                    Children = { name, details, source, path },
                });
        });

    private async Task LoadServersAsync() =>
        await RunActionAsync(
            async cancellationToken =>
            {
                var source = (_serverSource.Text ?? string.Empty).Trim();
                if (source.Length == 0)
                {
                    _serverStatus.Text =
                        "Server-list refresh is disabled. Enter an HTTPS source or 'cache'.";
                    return;
                }

                var list = await _session.LoadServerListAsync(
                        source,
                        cancellationToken);
                UpdateServerRows();
                _serverStatus.Text =
                    $"{list.Servers.Count} server endpoint(s) loaded.";
            });

    private async Task ConnectServerAsync() =>
        await RunActionAsync(
            async cancellationToken =>
            {
                if (_session.ServerList is null)
                {
                    var source = (_serverSource.Text ?? string.Empty).Trim();
                    if (source.Length == 0)
                    {
                        _serverStatus.Text =
                            "Server-list refresh is disabled. Load a list before connecting.";
                        return;
                    }

                    var list = await _session.LoadServerListAsync(
                            source,
                            cancellationToken);
                    UpdateServerRows();
                    _serverStatus.Text =
                        $"{list.Servers.Count} server endpoint(s) loaded.";
                }

                var connection = _selectedServer is { } selected
                    ? await _session.ConnectServerAsync(
                        selected.Host,
                        selected.Port,
                        cancellationToken)
                    : await _session.ConnectServerAsync(
                        cancellationToken: cancellationToken);
                _serverStatus.Text =
                    $"Connected: {connection.Server} · "
                    + FormatConnectionLevel(
                        connection.IsHighId,
                        connection.AssignedPort);
            });

    private void UpdateServerRows()
    {
        var list = _session.ServerList;
        if (list is null)
            return;

        _selectedServer = null;
        _serverRows.Clear();
        foreach (var server in list.Servers)
        {
            var name = string.IsNullOrWhiteSpace(server.Name)
                ? "Unnamed server"
                : server.Name;
            var users = server.UserCount is { } userCount
                ? $" · {userCount:N0} users"
                : string.Empty;
            var files = server.FileCount is { } fileCount
                ? $" · {fileCount:N0} files"
                : string.Empty;
            _serverRows.Add(
                new NmuleServerRow(
                    name,
                    server.Endpoint.ToString(),
                    $"{name}\n{server.Endpoint}{users}{files}",
                    server.Endpoint.Host,
                    server.Endpoint.Port));
        }

        _serverList.IsVisible = _serverRows.Count > 0;
    }

    private void OnServerSelectionChanged(
        object? sender,
        SelectionChangedEventArgs args)
    {
        _selectedServer = args.CurrentSelection.FirstOrDefault()
            as NmuleServerRow;
        if (_selectedServer is not null)
        {
            _serverStatus.Text =
                $"Selected {_selectedServer.Name} · "
                    + $"{_selectedServer.Endpoint}. Press Connect.";
        }
    }

    private static string FormatConnectionLevel(
        bool isHighId,
        int assignedPort) =>
        isHighId
            ? "HighID."
            : $"LowID on port {assignedPort}. Improve this by using a VPN "
              + "with port forwarding, forwarding nMule's TCP and Kad UDP "
              + "ports, and allowing them through the local firewall. "
              + "This MVP cannot host LowID callbacks.";

    private async Task BootstrapKadAsync() =>
        await RunActionAsync(
            async cancellationToken =>
            {
                var source = (_kadSource.Text ?? string.Empty).Trim();
                if (source.Length == 0)
                {
                    _kadStatus.Text =
                        "Kad bootstrap is disabled. Enter an HTTPS source or 'cache'.";
                    return;
                }

                var contacts = await _session.BootstrapKadAsync(
                        source,
                        cancellationToken);
                _kadStatus.Text =
                    $"Kad ready with {contacts.Count} known contact(s).";
            });

    private async Task SearchAsync() =>
        await RunActionAsync(
            async cancellationToken =>
            {
                var hits = await _session.SearchAsync(
                        _query.Text ?? string.Empty,
                        cancellationToken);
                _allSearchResults = hits.ToArray();
                UpdateResultTypeOptions();
                ApplyResultFilters();
            });

    private void UpdateResultTypeOptions()
    {
        var types = new[] { AllTypesFilter }
            .Concat(
                _allSearchResults
                    .Select(static hit => hit.TypeLabel)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(static type => type, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        _resultTypeFilter.ItemsSource = types;
        _resultTypeFilter.SelectedIndex = 0;
    }

    private void ApplyResultFilters()
    {
        var text = (_resultFilter.Text ?? string.Empty).Trim();
        var type = _resultTypeFilter.SelectedItem as string;
        var availability = _resultAvailabilityFilter.SelectedItem as string;
        var speed = _resultSpeedFilter.SelectedItem as string;
        var transfer = _resultTransferFilter.SelectedItem as string;
        var sort = _resultSort.SelectedItem as string;
        IEnumerable<NmuleSearchHit> filtered = _allSearchResults;

        if (text.Length > 0)
        {
            filtered = filtered.Where(
                hit => hit.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                    || hit.TypeLabel.Contains(
                        text,
                        StringComparison.OrdinalIgnoreCase)
                    || hit.FileId.ToString().Contains(
                        text,
                        StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(type)
            && !string.Equals(type, AllTypesFilter, StringComparison.Ordinal))
        {
            filtered = filtered.Where(
                hit => string.Equals(
                    hit.TypeLabel,
                    type,
                    StringComparison.OrdinalIgnoreCase));
        }

        filtered = availability switch
        {
            "Has advertised sources" => filtered.Where(
                static hit => hit.SourceCount > 0),
            "No advertised sources" => filtered.Where(
                static hit => hit.SourceCount <= 0),
            _ => filtered,
        };
        filtered = speed switch
        {
            "Speed advertised" => filtered.Where(
                static hit => hit.SpeedBytesPerSecond is > 0),
            "Speed not advertised" => filtered.Where(
                static hit => hit.SpeedBytesPerSecond is not > 0),
            _ => filtered,
        };
        filtered = transfer switch
        {
            "Direct download" => filtered.Where(
                static hit => hit.Eligibility
                    == NmuleTransferEligibility.DirectHighId),
            "Kad source lookup" => filtered.Where(
                static hit => hit.Eligibility
                    == NmuleTransferEligibility.KadSourceRequired),
            "Unavailable" => filtered.Where(
                static hit => hit.Eligibility
                    == NmuleTransferEligibility.InvalidSource),
            _ => filtered,
        };

        filtered = sort switch
        {
            "Name A-Z" => filtered.OrderBy(
                static hit => hit.Name,
                StringComparer.OrdinalIgnoreCase),
            "Name Z-A" => filtered.OrderByDescending(
                static hit => hit.Name,
                StringComparer.OrdinalIgnoreCase),
            "Smallest first" => filtered.OrderBy(static hit => hit.Size),
            "Largest first" => filtered.OrderByDescending(static hit => hit.Size),
            "Most sources" => filtered.OrderByDescending(
                static hit => hit.SourceCount),
            "Fewest sources" => filtered.OrderBy(static hit => hit.SourceCount),
            "Fastest advertised" => filtered.OrderByDescending(
                static hit => hit.SpeedBytesPerSecond ?? -1),
            "Slowest advertised" => filtered.OrderBy(
                static hit => hit.SpeedBytesPerSecond ?? long.MaxValue),
            _ => filtered,
        };

        var visible = filtered.ToArray();
        _searchResults.Clear();
        foreach (var hit in visible)
            _searchResults.Add(hit);
        _resultStatus.Text =
            $"{visible.Length:N0} of {_allSearchResults.Count:N0} result(s) shown.";
    }

    private async Task DownloadAsync(NmuleSearchHit hit) =>
        await RunActionAsync(
            async cancellationToken =>
            {
                SetOperationStatus("Preparing download...");
                var result = await _session.DownloadAsync(hit, cancellationToken);
                _downloadResults.Add(result);
                _downloadStatus.Text =
                    $"{_downloadResults.Count} completed download(s).";
                SetOperationStatus("Download completed.");
                _navigationShell.Controller.SelectPrimary("nmule.downloads");
            });

    private Task DownloadFromButtonAsync(Button? button)
    {
        if (button?.BindingContext is not NmuleSearchHit hit)
        {
            SetOperationStatus("This result is no longer available.");
            return Task.CompletedTask;
        }

        if (hit.Eligibility is NmuleTransferEligibility.InvalidSource)
        {
            SetOperationStatus(
                "This result is unavailable for the current transfer mode.");
            return Task.CompletedTask;
        }

        return DownloadAsync(hit);
    }

    private async Task RunActionAsync(
        Func<CancellationToken, Task> action)
    {
        if (_busy)
        {
            SetOperationStatus("Another operation is already in progress.");
            return;
        }

        _busy = true;
        try
        {
            SetOperationStatus("Working...");
            using var timeout = new CancellationTokenSource(
                TimeSpan.FromMinutes(2));
            await action(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            SetOperationStatus("Operation cancelled.");
        }
        catch (NmuleNetworkBlockedException)
        {
            SetOperationStatus(
                "Network access is blocked by the current VPN policy.");
            await RefreshNetworkStatusAsync();
        }
        catch (NotSupportedException)
        {
            SetOperationStatus(
                "This operation is not available for the selected result.");
        }
        catch (InvalidOperationException)
        {
            SetOperationStatus(
                "nMule could not complete the operation. "
                + "Check the connection and required Kad state.");
        }
        catch (Exception)
        {
            SetOperationStatus("nMule could not complete the operation.");
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task RefreshNetworkStatusAsync()
    {
        try
        {
            var decision = await _session.NetworkGate.CheckAsync();
            await MainThread.InvokeOnMainThreadAsync(
                () =>
                {
                    var status = decision.IsAllowed
                        ? _session.NetworkGate.AllowUnrestrictedNetwork
                            ? "VPN requirement bypassed for this session. This is not recommended."
                            : _session.NetworkGate.AllowTailscale
                            ? "VPN gate ready; the explicit Tailscale override is on."
                            : "VPN gate ready; Tailscale remains blocked."
                        : decision.DenialReason
                          ?? "VPN gate is blocking network access.";
                    _networkStatus.Text = status;
                    _searchNetworkStatus.Text = status;
                });
        }
        catch (Exception)
        {
            var status = "VPN policy check failed.";
            _networkStatus.Text = status;
            _searchNetworkStatus.Text = status;
        }
    }

    private void OnSessionStatusChanged(string status) =>
        MainThread.BeginInvokeOnMainThread(() => SetOperationStatus(status));

    private void SetOperationStatus(string status)
    {
        _status.Text = status;
        _actionStatus.Text = status;
    }

    private void OnNavigationChanged(
        object? sender,
        MobileNavigationChangedEventArgs args)
    {
        ApplyRoute(args.Current.CurrentRouteId ?? "search");
    }

    private void ApplyRoute(string route)
    {
        _searchView.IsVisible = route == "search";
        _downloadsView.IsVisible = route == "downloads";
        _settingsView.IsVisible = route == "settings";
        _navigationShell.Title = route switch
        {
            "downloads" => "nMule · Downloads",
            "settings" => "nMule · Settings",
            _ => "nMule · Search",
        };
    }

    private void OnRequestedThemeChanged(
        object? sender,
        AppThemeChangedEventArgs args)
    {
        ApplyTheme();
        _navigationShell.ApplyTheme();
    }

    private void ApplyTheme()
    {
        BackgroundColor = Profile.Background;
        _status.TextColor = Profile.Text;
        _networkStatus.TextColor = Profile.Text;
        _searchNetworkStatus.TextColor = Profile.Text;
        _serverStatus.TextColor = Profile.Muted;
        _kadStatus.TextColor = Profile.Muted;
        _downloadStatus.TextColor = Profile.Muted;
    }

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
            BackgroundColor = Profile.Raised,
            PlaceholderColor = Profile.Muted,
            ClearButtonVisibility = ClearButtonVisibility.WhileEditing,
        };

    private static Picker CreatePicker(
        string title,
        string automationId,
        string[] items) =>
        new()
        {
            AutomationId = automationId,
            Title = title,
            ItemsSource = items,
            SelectedIndex = 0,
            TextColor = Profile.Text,
            BackgroundColor = Profile.Raised,
            MinimumHeightRequest = GraphicalProfileColors.TouchTarget,
        };

    private static Button CreateButton(
        string text,
        string automationId,
        Func<Task> action)
    {
        var button = new Button
        {
            AutomationId = automationId,
            Text = text,
            FontFamily = Profile.FontFamily,
            FontAttributes = FontAttributes.Bold,
            MinimumHeightRequest = GraphicalProfileColors.TouchTarget,
            BackgroundColor = Profile.Action,
            TextColor = Profile.OnAction,
            BorderColor = Profile.Border,
            BorderWidth = GraphicalProfileColors.Stroke,
            CornerRadius = (int)GraphicalProfileColors.ControlRadius,
            Padding = new Thickness(14, 8),
        };
        SemanticProperties.SetDescription(button, text);
        button.Clicked += async (_, _) => await action();
        return button;
    }

    private static Border CreateCard(View content) =>
        new()
        {
            Padding = new Thickness(GraphicalProfileColors.CardPadding),
            StrokeThickness = GraphicalProfileColors.Stroke,
            Stroke = new SolidColorBrush(Profile.Border),
            StrokeShape = new RoundRectangle
            {
                CornerRadius = new CornerRadius(GraphicalProfileColors.CardRadius),
            },
            Background = new SolidColorBrush(Profile.Surface),
            Content = content,
        };

    private static Label CreateSectionTitle(string text) =>
        new()
        {
            Text = text,
            FontFamily = Profile.FontFamily,
            FontAttributes = FontAttributes.Bold,
            FontSize = GraphicalProfileColors.SectionTitleSize,
            TextColor = Profile.Text,
        };
}
