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
    private readonly NmuleSession _session;
    private readonly MobileNavigationShell _navigationShell;
    private readonly Entry _serverSource;
    private readonly Entry _kadSource;
    private readonly Entry _query;
    private readonly Label _status;
    private readonly Label _networkStatus;
    private readonly Label _serverStatus;
    private readonly Label _kadStatus;
    private readonly Label _downloadStatus;
    private readonly CollectionView _results;
    private readonly CollectionView _downloads;
    private readonly Switch _allowTailscale;
    private readonly ObservableCollection<NmuleSearchHit> _searchResults = [];
    private readonly ObservableCollection<NmuleDownloadResult> _downloadResults = [];
    private readonly View _searchView;
    private readonly View _downloadsView;
    private readonly View _settingsView;
    private bool _busy;
    private bool _themeSubscribed;

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
        _networkStatus = new Label
        {
            AutomationId = "NmuleNetworkStatus",
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

        _allowTailscale = new Switch
        {
            AutomationId = "NmuleAllowTailscale",
            IsVisible = _session.NetworkGate.SupportsTailscaleOverride,
            IsToggled = _session.NetworkGate.AllowTailscale,
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

        _results = new CollectionView
        {
            AutomationId = "NmuleSearchResults",
            ItemsSource = _searchResults,
            SelectionMode = SelectionMode.None,
            HeightRequest = 380,
            ItemTemplate = CreateSearchTemplate(),
        };
        _downloads = new CollectionView
        {
            AutomationId = "NmuleDownloads",
            ItemsSource = _downloadResults,
            SelectionMode = SelectionMode.None,
            ItemTemplate = CreateDownloadTemplate(),
        };

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
                    _results,
                },
            });

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
                    BuildNetworkPolicyCard(),
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
        var card = CreateCard(
            new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    CreateSectionTitle("Network policy"),
                    _networkStatus,
                    new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(GridLength.Auto),
                        },
                        ColumnSpacing = 12,
                        Children =
                        {
                            new Label
                            {
                                Text = "Allow Tailscale for this session",
                                VerticalTextAlignment = TextAlignment.Center,
                            },
                            _allowTailscale,
                        },
                    },
                    new Label
                    {
                        Text = "VPN access is checked before DNS, HTTPS, TCP, and UDP activity. The override is off again when nMule restarts.",
                        FontSize = GraphicalProfileColors.CaptionSize,
                        TextColor = Profile.Muted,
                        LineBreakMode = LineBreakMode.WordWrap,
                    },
                },
            });
        Grid.SetColumn(_allowTailscale, 1);
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
                    CreateCard(
                        new VerticalStackLayout
                        {
                            Spacing = 8,
                            Children =
                            {
                                CreateSectionTitle("About nMule"),
                                new Label
                                {
                                    Text = "nMule is a foreground Android eD2K/Kad client. It uses the clean-room Novolis transport packages and does not start background transfers.",
                                    LineBreakMode = LineBreakMode.WordWrap,
                                },
                                new Label
                                {
                                    Text = "The first transfer supports one verified eD2K part. Larger files, LowID callback hosting, uploads, and resume scheduling are intentionally unavailable in this MVP.",
                                    FontSize = GraphicalProfileColors.CaptionSize,
                                    TextColor = Profile.Muted,
                                    LineBreakMode = LineBreakMode.WordWrap,
                                },
                            },
                        }),
                },
            },
        };

    private DataTemplate CreateSearchTemplate() =>
        new(() =>
        {
            var name = new Label
            {
                FontAttributes = FontAttributes.Bold,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1,
            };
            name.SetBinding(Label.TextProperty, nameof(NmuleSearchHit.Name));
            var details = new Label
            {
                FontSize = GraphicalProfileColors.CaptionSize,
                TextColor = Profile.Muted,
            };
            details.SetBinding(
                Label.TextProperty,
                new Binding(
                    nameof(NmuleSearchHit.Size),
                    stringFormat: "{0:N0} bytes"));
            var eligibility = new Label
            {
                FontSize = GraphicalProfileColors.CaptionSize,
                TextColor = Profile.Muted,
            };
            eligibility.SetBinding(
                Label.TextProperty,
                nameof(NmuleSearchHit.EligibilityLabel));
            var download = CreateButton(
                "Download",
                "NmuleDownloadResult",
                () => Task.CompletedTask);
            download.Clicked += async (sender, _) =>
            {
                if ((sender as Button)?.BindingContext is NmuleSearchHit hit)
                    await DownloadAsync(hit);
            };
            return CreateCard(
                new VerticalStackLayout
                {
                    Spacing = 4,
                    Children =
                    {
                        name,
                        details,
                        eligibility,
                        download,
                    },
                });
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
            return CreateCard(
                new VerticalStackLayout
                {
                    Spacing = 4,
                    Children = { name, details, source },
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
                    _serverStatus.Text =
                        $"{list.Servers.Count} server endpoint(s) loaded.";
                }

                var connection = await _session.ConnectServerAsync(
                        cancellationToken: cancellationToken);
                _serverStatus.Text =
                    $"Connected: {connection.Server} · "
                    + $"{(connection.IsHighId ? "HighID" : "LowID")}.";
            });

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
                _searchResults.Clear();
                foreach (var hit in hits)
                    _searchResults.Add(hit);
            });

    private async Task DownloadAsync(NmuleSearchHit hit) =>
        await RunActionAsync(
            async cancellationToken =>
            {
                var result = await _session.DownloadAsync(hit, cancellationToken);
                _downloadResults.Add(result);
                _downloadStatus.Text =
                    $"{_downloadResults.Count} completed download(s).";
                _navigationShell.Controller.SelectPrimary("nmule.downloads");
            });

    private async Task RunActionAsync(
        Func<CancellationToken, Task> action)
    {
        if (_busy)
            return;

        _busy = true;
        try
        {
            using var timeout = new CancellationTokenSource(
                TimeSpan.FromMinutes(2));
            await action(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Operation cancelled.";
        }
        catch (NmuleNetworkBlockedException exception)
        {
            _status.Text = exception.Message;
            await RefreshNetworkStatusAsync();
        }
        catch (Exception exception)
        {
            _status.Text = $"nMule could not complete the operation: {exception.Message}";
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
                    _networkStatus.Text = decision.IsAllowed
                        ? _session.NetworkGate.AllowTailscale
                            ? "VPN gate ready; the explicit Tailscale override is on."
                            : "VPN gate ready; Tailscale remains blocked."
                        : decision.DenialReason
                          ?? "VPN gate is blocking network access.";
                });
        }
        catch (Exception exception)
        {
            _networkStatus.Text = $"VPN policy check failed: {exception.Message}";
        }
    }

    private void OnSessionStatusChanged(string status) =>
        MainThread.BeginInvokeOnMainThread(() => _status.Text = status);

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
