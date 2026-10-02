using Novolis.Avalonia.GraphicalProfile;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Avalonia.Map;
using Novolis.Avalonia.Mobile;
using Novolis.Math.Geometry;
using PresenceLedger.App.Map;
using PresenceLedger.Core;
using PresenceLedger.Storage;

namespace PresenceLedger.App;

/// <summary>Small v1 shell for locations, setup, history, and diagnostics.</summary>
public sealed class MainView : UserControl
{
    static readonly GeoCoordinate DefaultMapCenter = new(58.14623, 7.99517);

    readonly ITrackedLocationStore _locations;
    readonly IPresenceEventStore _events;
    readonly IPresenceStateStore _states;
    readonly IPresenceObservationStore _observations;
    readonly PresenceDayProjector _dayProjector;
    readonly IMapTileSource _tileSource;
    readonly IMapSearchProvider _searchProvider;
    readonly IServiceProvider _services;
    readonly ContentControl _content = new();
    readonly TextBlock _status = new();

    DateOnly _displayDate = DateOnly.FromDateTime(DateTime.Now);
    GeoCoordinate? _selectedCoordinate;
    TrackedLocation? _editingLocation;
    TextBox? _nameInput;
    TextBox? _ssidInput;
    TextBox? _coordinateInput;
    TextBox? _effectiveFromInput;
    Slider? _radiusInput;
    TextBlock? _radiusLabel;
    MapControl? _pickerMap;

    /// <summary>Creates the shared product view.</summary>
    public MainView(
        ITrackedLocationStore locations,
        IPresenceEventStore events,
        IPresenceStateStore states,
        IPresenceObservationStore observations,
        PresenceDayProjector dayProjector,
        IMapTileSource tileSource,
        IMapSearchProvider searchProvider,
        IServiceProvider services)
    {
        _locations = locations ?? throw new ArgumentNullException(nameof(locations));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _states = states ?? throw new ArgumentNullException(nameof(states));
        _observations = observations ?? throw new ArgumentNullException(nameof(observations));
        _dayProjector = dayProjector ?? throw new ArgumentNullException(nameof(dayProjector));
        _tileSource = tileSource ?? throw new ArgumentNullException(nameof(tileSource));
        _searchProvider = searchProvider ?? throw new ArgumentNullException(nameof(searchProvider));
        _services = services ?? throw new ArgumentNullException(nameof(services));

        Background = GraphicalProfile.BackgroundBrush;
        BuildShell();
        AttachedToVisualTree += async (_, _) => await ShowTodayAsync();
    }

    void BuildShell()
    {
        var header = new StackPanel
        {
            Margin = new Thickness(16, 14, 16, 10),
            Spacing = 10,
        };

        var brandMark = new Border
        {
            Width = 48,
            Height = 48,
            Background = GraphicalProfile.AccentFillBrush,
            BorderBrush = GraphicalProfile.AccentBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Child = new TextBlock
            {
                Text = "P",
                FontSize = 25,
                FontWeight = FontWeight.Bold,
                Foreground = GraphicalProfile.TextBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        var brandCopy = new StackPanel
        {
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = "PRESENCE LEDGER",
                    FontSize = 17,
                    FontWeight = FontWeight.Bold,
                    Foreground = GraphicalProfile.TextBrush,
                },
                new TextBlock
                {
                    Text = "A quiet record of your places",
                    FontSize = 11,
                    Foreground = GraphicalProfile.MutedBrush,
                },
            },
        };
        var localBadge = new Border
        {
            Padding = new Thickness(10, 6),
            Background = GraphicalProfile.SurfaceBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = "LOCAL",
                Classes = { "eyebrow" },
                Foreground = GraphicalProfile.AccentBrush,
            },
        };
        var brandRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 12,
        };
        brandRow.Children.Add(brandMark);
        Grid.SetColumn(brandMark, 0);
        brandRow.Children.Add(brandCopy);
        Grid.SetColumn(brandCopy, 1);
        brandRow.Children.Add(localBadge);
        Grid.SetColumn(localBadge, 2);
        header.Children.Add(brandRow);

        var navigation = new StackPanel
        {
            Orientation = Orientation.Horizontal,
        };
        navigation.Children.Add(NavigationButton("Today", async () => await ShowTodayAsync()));
        navigation.Children.Add(NavigationButton("Map", async () => await ShowMapAsync()));
        navigation.Children.Add(NavigationButton("Places", async () => await ShowLocationsAsync()));
        navigation.Children.Add(NavigationButton("History", async () => await ShowHistoryAsync()));
        navigation.Children.Add(NavigationButton("More", async () => await ShowDiagnosticsAsync()));
        var navigationScroll = new ScrollViewer
        {
            Content = navigation,
            HorizontalScrollBarVisibility =
                Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility =
                Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        header.Children.Add(navigationScroll);

        _status.Text = "Local storage · map and address search use Kartverket services";
        _status.Foreground = GraphicalProfile.MutedBrush;
        _status.FontSize = 11;
        _status.Margin = new Thickness(16, 4);

        var shell = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(_status, Dock.Bottom);
        shell.Children.Add(header);
        shell.Children.Add(_status);
        shell.Children.Add(_content);
        Content = shell;
    }

    static Button NavigationButton(string label, Func<Task> action)
    {
        var button = new Button
        {
            Content = label,
            Classes = { "nav-button" },
            Background = GraphicalProfile.SurfaceBrush,
            Foreground = GraphicalProfile.TextBrush,
        };
        button.Click += async (_, _) => await action();
        return button;
    }

    async Task ShowTodayAsync()
    {
        try
        {
            var projection = await ReadDayProjectionAsync(_displayDate);
            var locations = await ReadAllAsync(_locations.ReadAsync());
            _content.Content = PresenceTodayView.Build(
                projection,
                locations,
                DateTimeOffset.UtcNow,
                () => _ = ChangeDayAsync(-1),
                () => _ = ChangeDayAsync(1),
                () => _ = ChangeDayAsync(
                    DateOnly.FromDateTime(DateTime.Now).DayNumber - _displayDate.DayNumber),
                () => _ = ShowMapAsync(),
                () => _ = ShowAddLocationAsync(),
                GetObservationStatusText());
            SetStatus(
                $"{projection.Observations.Count} local sample"
                + $"{(projection.Observations.Count == 1 ? string.Empty : "s")} · "
                + $"{projection.Intervals.Count} confirmed segment"
                + $"{(projection.Intervals.Count == 1 ? string.Empty : "s")}");
        }
        catch (Exception ex)
        {
            _content.Content = ErrorSurface(
                "Today is unavailable",
                "The local ledger could not be projected.",
                ex.Message,
                () => _ = ShowTodayAsync());
            SetStatus($"Could not read today: {ex.Message}");
        }
    }

    async Task ChangeDayAsync(int dayOffset)
    {
        _displayDate = _displayDate.AddDays(dayOffset);
        await ShowTodayAsync();
    }

    string GetObservationStatusText()
    {
        var location = _services.GetService<ILocationReadingSource>();
        var wifi = _services.GetService<IWifiObservationSource>();
        var coordinator = _services.GetService<PresenceObservationCoordinator>();
        if (location is null && wifi is null)
            return "Desktop history mode · local data is available here, background observation is not.";

        var statuses = new[]
        {
            location?.GetStatus(),
            wifi?.GetStatus(),
        }
        .Where(status => status is not null)
        .Select(status => FormatSourceStatus(status!.Value))
        .ToArray();
        var service = coordinator?.IsRunning == true
            ? "observer active"
            : "observer paused";
        return statuses.Length == 0
            ? $"Local {service}."
            : $"Local {service} · {string.Join(" · ", statuses)}";
    }

    async Task<PresenceDayProjection> ReadDayProjectionAsync(DateOnly displayDate)
    {
        var displayTimeZone = TimeZoneInfo.Local;
        var localStart = displayDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var localEnd = displayDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, displayTimeZone);
        var utcEnd = TimeZoneInfo.ConvertTimeToUtc(localEnd, displayTimeZone);
        var observations = new List<PresenceObservationRecord>();
        for (var utcDate = DateOnly.FromDateTime(utcStart);
             utcDate <= DateOnly.FromDateTime(utcEnd.AddTicks(-1));
             utcDate = utcDate.AddDays(1))
        {
            await foreach (var observation in _observations.ReadAsync(utcDate))
                observations.Add(observation);
        }

        var events = await ReadAllAsync(_events.ReadAsync());
        var locationHistory = await ReadAllAsync(_locations.ReadHistoryAsync());
        return _dayProjector.Project(
            displayDate,
            displayTimeZone,
            events,
            observations,
            locationHistory);
    }

    static Control ErrorSurface(
        string title,
        string subtitle,
        string detail,
        Action retry)
    {
        var button = new Button
        {
            Content = "Try again",
            Classes = { "primary-button" },
            Background = GraphicalProfile.ActionBrush,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        button.Click += (_, _) => retry();
        return new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(16, 24),
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        Classes = { "page-title" },
                        Foreground = GraphicalProfile.TextBrush,
                    },
                    new TextBlock
                    {
                        Text = subtitle,
                        Classes = { "body-copy" },
                        Foreground = GraphicalProfile.MutedBrush,
                    },
                    new TextBlock
                    {
                        Text = detail,
                        Foreground = GraphicalProfile.DangerBrush,
                        TextWrapping = TextWrapping.Wrap,
                    },
                    button,
                },
            },
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            HorizontalScrollBarVisibility =
                Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility =
                Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };
    }

    async Task ShowLocationsAsync()
    {
        try
        {
            var locations = await ReadAllAsync(_locations.ReadAsync());
            var stack = PageStack(
                "Your places",
                "Home, Work, and the other places that make your day legible.");
            var add = new Button
            {
                Content = "Add location",
                Classes = { "primary-button" },
                Background = GraphicalProfile.ActionBrush,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            add.Click += async (_, _) => await ShowAddLocationAsync();
            stack.Children.Add(add);
            if (locations.Count == 0)
            {
                stack.Children.Add(Card(
                    new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "Nothing configured yet.",
                                FontSize = 18,
                                FontWeight = FontWeight.SemiBold,
                                Foreground = GraphicalProfile.TextBrush,
                            },
                            new TextBlock
                            {
                                Text = "Add a place with a map point, radius, and optional Wi-Fi evidence.",
                                Foreground = GraphicalProfile.MutedBrush,
                                TextWrapping = TextWrapping.Wrap,
                            },
                        },
                    },
                    GraphicalProfile.SurfaceBrush));
            }
            else
            {
                foreach (var location in locations)
                    stack.Children.Add(await BuildLocationCardAsync(location));
            }

            _content.Content = new ScrollViewer
            {
                Content = stack,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            };
            SetStatus($"{locations.Count} place{(locations.Count == 1 ? string.Empty : "s")} configured");
        }
        catch (Exception ex)
        {
            _content.Content = ErrorSurface(
                "Places are unavailable",
                "The local location ledger could not be read.",
                ex.Message,
                () => _ = ShowLocationsAsync());
            SetStatus($"Could not read locations: {ex.Message}");
        }
    }

    static Border Card(Control child, IBrush background) =>
        new()
        {
            Child = child,
            Padding = new Thickness(18),
            Background = background,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
        };

    async Task ShowMapAsync()
    {
        try
        {
            var locations = await ReadAllAsync(_locations.ReadAsync());
            var projection = await ReadDayProjectionAsync(_displayDate);
            var center = locations.FirstOrDefault()?.Area.Center
                ?? projection.Observations
                    .Select(item => item.Position)
                    .FirstOrDefault(position => position is not null)
                ?? DefaultMapCenter;
            var map = new MapControl
            {
                Viewport = new MapViewport(center, locations.Count == 0 ? 12 : 14),
                TileSource = _tileSource,
                Attribution = KartverketMap.Attribution,
                MinHeight = 280,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                BackgroundBrush = GraphicalProfile.SurfaceBrush,
            };
            map.Markers = locations
                .Select(location => new MapMarker(
                    location.Id.ToString("N"),
                    location.Area.Center,
                    location.DisplayName,
                    8))
                .Concat(projection.Observations
                    .Where(item => item.Position is not null)
                    .Select((item, index) => new MapMarker(
                        $"sample-{index}",
                        item.Position!.Value,
                        null,
                        4)))
                .ToArray();
            var sampleTrack = projection.Observations
                .Where(item => item.Position is not null)
                .Select(item => item.Position!.Value)
                .ToArray();
            map.Tracks = sampleTrack.Length < 2
                ? []
                : [new MapTrackOverlay("day-samples", sampleTrack, "Local samples")];
            map.Circles = locations
                .Select(location => new MapCircleOverlay(
                    location.Id.ToString("N"),
                    location.Area,
                    location.DisplayName))
                .ToArray();
            var zoomIn = new Button
            {
                Content = "+",
                MinWidth = 44,
                MinHeight = 44,
                Background = GraphicalProfile.SurfaceBrush,
                Foreground = GraphicalProfile.TextBrush,
            };
            zoomIn.Click += (_, _) => map.ZoomIn();
            var zoomOut = new Button
            {
                Content = "−",
                MinWidth = 44,
                MinHeight = 44,
                Background = GraphicalProfile.SurfaceBrush,
                Foreground = GraphicalProfile.TextBrush,
            };
            zoomOut.Click += (_, _) => map.ZoomOut();
            var fit = new Button
            {
                Content = "Fit",
                MinHeight = 44,
                Background = GraphicalProfile.SurfaceBrush,
                Foreground = GraphicalProfile.TextBrush,
            };
            fit.Click += (_, _) => map.FitToContent(
                locations
                    .Select(location => location.Area.Center)
                    .Concat(sampleTrack));
            var refresh = new Button
            {
                Content = "Refresh",
                MinWidth = 44,
                MinHeight = 44,
                Background = GraphicalProfile.SurfaceBrush,
                Foreground = GraphicalProfile.TextBrush,
            };
            refresh.Click += async (_, _) =>
            {
                try
                {
                    await map.RefreshTilesAsync();
                    SetStatus("Map refreshed");
                }
                catch (Exception ex)
                {
                    SetStatus($"Map unavailable: {ex.Message}");
                }
            };
            var mapFrame = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Children =
                {
                    map,
                    new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        Spacing = 6,
                        Margin = new Thickness(10),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Top,
                        Children = { zoomIn, zoomOut, fit, refresh },
                    },
                },
            };
            var header = new StackPanel
            {
                Spacing = 4,
                Margin = new Thickness(0, 0, 0, 8),
                Children =
                {
                    new TextBlock
                    {
                        Text = "DAY MAP",
                        Classes = { "eyebrow" },
                        Foreground = GraphicalProfile.AccentBrush,
                    },
                    new TextBlock
                    {
                        Text = "Where the day took shape",
                        Classes = { "page-title" },
                        Foreground = GraphicalProfile.TextBrush,
                    },
                    new TextBlock
                    {
                        Text = $"{_displayDate:yyyy-MM-dd} · two fingers pan and pinch. Tiles stay in the map; samples stay on this device.",
                        Foreground = GraphicalProfile.MutedBrush,
                        TextWrapping = TextWrapping.Wrap,
                    },
                },
            };
            var page = new Grid
            {
                Margin = new Thickness(16, 12, 16, 12),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                RowDefinitions =
                {
                    new RowDefinition(GridLength.Auto),
                    new RowDefinition(new GridLength(1, GridUnitType.Star)),
                },
            };
            page.Children.Add(header);
            Grid.SetRow(header, 0);
            page.Children.Add(mapFrame);
            Grid.SetRow(mapFrame, 1);
            _content.Content = page;
            try
            {
                await map.RefreshTilesAsync();
                SetStatus("Map ready");
            }
            catch (Exception ex)
            {
                SetStatus($"Map unavailable: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            _content.Content = ErrorSurface(
                "Map is unavailable",
                "The local day data could not be loaded.",
                ex.Message,
                () => _ = ShowMapAsync());
            SetStatus($"Could not read map data: {ex.Message}");
        }
    }

    async Task<Control> BuildLocationCardAsync(TrackedLocation location)
    {
        var state = await _states.GetAsync(location.Id) ?? LocationPresenceState.CreateAbsent(location.Id);
        var stateText = state.State switch
        {
            PresenceState.Present => $"Present since {state.LastEvidenceAt?.ToLocalTime():g}",
            PresenceState.CandidatePresent => "Checking arrival evidence…",
            PresenceState.CandidateAbsent => "Checking departure evidence…",
            _ => "Not present",
        };
        var evidenceText = location.Wifi is null
            ? "Location evidence"
            : $"Location + Wi-Fi · {location.Wifi.Ssid}";
        var content = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children =
            {
                new TextBlock
                {
                    Text = location.DisplayName,
                    FontSize = 18,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = GraphicalProfile.TextBrush,
                },
                new TextBlock
                {
                    Text = stateText,
                    Foreground = state.State == PresenceState.Present
                        ? GraphicalProfile.AccentBrush
                        : GraphicalProfile.MutedBrush,
                    TextWrapping = TextWrapping.Wrap,
                },
                new TextBlock
                {
                    Text = $"{evidenceText} · {location.Area.RadiusMeters:0} m radius",
                    Foreground = GraphicalProfile.MutedBrush,
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };
        var edit = new Button
        {
            Content = "Edit location",
            HorizontalAlignment = HorizontalAlignment.Left,
            MinHeight = 44,
            Margin = new Thickness(0, 8, 0, 0),
            Padding = new Thickness(12, 7),
            Background = GraphicalProfile.RaisedBrush,
            Foreground = GraphicalProfile.TextBrush,
        };
        edit.Click += async (_, _) => await ShowAddLocationAsync(location);
        content.Children.Add(edit);
        return new Border
        {
            Child = content,
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 10),
            Background = GraphicalProfile.SurfaceBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
        };
    }

    async Task ShowAddLocationAsync(TrackedLocation? existing = null)
    {
        _editingLocation = existing;
        _selectedCoordinate = existing?.Area.Center;
        _nameInput = new TextBox
        {
            Text = existing?.DisplayName ?? string.Empty,
            PlaceholderText = "Display name",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 48,
        };
        _effectiveFromInput = new TextBox
        {
            Text = existing?.EffectiveFromUtc?.ToString("O")
                ?? DateTimeOffset.UtcNow.ToString("O"),
            PlaceholderText = "2026-09-29T07:00:00Z",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 48,
        };
        _ssidInput = new TextBox
        {
            Text = existing?.Wifi?.Ssid ?? string.Empty,
            PlaceholderText = "Optional Wi-Fi SSID",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 48,
        };
        _coordinateInput = new TextBox
        {
            Text = FormatCoordinate(_selectedCoordinate ?? DefaultMapCenter),
            PlaceholderText = "Latitude, longitude",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 48,
        };
        _radiusInput = new Slider
        {
            Minimum = 50,
            Maximum = 1_000,
            Value = existing?.Area.RadiusMeters ?? 200,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        _radiusLabel = new TextBlock { Foreground = GraphicalProfile.MutedBrush };
        _radiusInput.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty && _radiusLabel is not null)
            {
                _radiusLabel.Text = $"Radius: {_radiusInput.Value:0} m";
                UpdatePickerOverlays();
            }
        };
        _radiusLabel.Text = "Radius: 200 m";

        _pickerMap = new MapControl
        {
            Viewport = new MapViewport(existing?.Area.Center ?? DefaultMapCenter, existing is null ? 13 : 15),
            TileSource = _tileSource,
            Attribution = KartverketMap.Attribution,
            Height = 320,
            MinHeight = 240,
            MaxHeight = 420,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        _pickerMap.SizeChanged += (_, e) =>
        {
            if (e.NewSize.Width <= 0)
                return;

            var height = global::System.Math.Clamp(e.NewSize.Width * 0.7, 240, 420);
            if (global::System.Math.Abs(_pickerMap.Height - height) > 1)
                _pickerMap.Height = height;
        };
        _pickerMap.PointSelected += coordinate =>
        {
            SetSelectedCoordinate(coordinate);
            UpdatePickerOverlays();
        };
        UpdatePickerOverlays();

        var searchInput = new TextBox
        {
            PlaceholderText = "Search an address (optional)",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 48,
        };
        var searchResults = new StackPanel { Spacing = 4 };
        var searchButton = new Button
        {
            Content = "Search address",
            Classes = { "primary-button" },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = GraphicalProfile.AccentFillBrush,
            Foreground = GraphicalProfile.TextBrush,
        };
        searchButton.Click += async (_, _) =>
        {
            searchResults.Children.Clear();
            if (string.IsNullOrWhiteSpace(searchInput.Text))
            {
                SetStatus("Enter an address to search.");
                return;
            }
            try
            {
                var results = await _searchProvider.SearchAsync(searchInput.Text);
                if (results.Count == 0)
                {
                    searchResults.Children.Add(new TextBlock
                    {
                        Text = "No addresses found.",
                        Foreground = GraphicalProfile.MutedBrush,
                    });
                }

                foreach (var result in results)
                {
                    var resultButton = new Button
                    {
                        Content = result.DisplayName,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                        MinHeight = 48,
                    };
                    resultButton.Click += (_, _) =>
                    {
                        SetSelectedCoordinate(result.Coordinate);
                        _pickerMap.Viewport = new MapViewport(result.Coordinate, 15);
                        UpdatePickerOverlays();
                    };
                    searchResults.Children.Add(resultButton);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Search unavailable: {ex.Message}");
            }
        };

        var save = new Button
        {
            Content = existing is null ? "Save location" : "Update location",
            Classes = { "primary-button" },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = GraphicalProfile.ActionBrush,
            Foreground = Brushes.White,
        };
        save.Click += async (_, _) => await SaveLocationAsync();

        var cancel = new Button
        {
            Content = "Cancel",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 48,
            Padding = new Thickness(14, 8),
            Background = GraphicalProfile.SurfaceBrush,
            Foreground = GraphicalProfile.TextBrush,
        };
        cancel.Click += async (_, _) => await ShowLocationsAsync();

        var stack = PageStack(
            existing is null ? "Add location" : "Edit location",
            "Choose a point and radius. Map content is used only during setup.");
        stack.Children.Add(new TextBlock { Text = "Name", Foreground = GraphicalProfile.MutedBrush });
        stack.Children.Add(_nameInput);
        stack.Children.Add(new TextBlock
        {
            Text = "Effective from (UTC)",
            Foreground = GraphicalProfile.MutedBrush,
        });
        stack.Children.Add(_effectiveFromInput);
        stack.Children.Add(new TextBlock
        {
            Text = "Use this to introduce a place retroactively without rewriting later revisions.",
            Foreground = GraphicalProfile.MutedBrush,
            TextWrapping = TextWrapping.Wrap,
        });
        stack.Children.Add(new TextBlock { Text = "Wi-Fi network (optional)", Foreground = GraphicalProfile.MutedBrush });
        stack.Children.Add(_ssidInput);
        stack.Children.Add(new TextBlock { Text = "Address search", Foreground = GraphicalProfile.MutedBrush });
        stack.Children.Add(new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children = { searchInput, searchButton },
        });
        stack.Children.Add(searchResults);
        stack.Children.Add(new TextBlock
        {
            Text = "Selected coordinates",
            Foreground = GraphicalProfile.MutedBrush,
        });
        stack.Children.Add(_coordinateInput);
        stack.Children.Add(new TextBlock
        {
            Text = "Enter latitude, longitude in decimal degrees, or tap the map.",
            Foreground = GraphicalProfile.MutedBrush,
            TextWrapping = TextWrapping.Wrap,
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Tap the map to set the center, then adjust the radius.",
            Foreground = GraphicalProfile.MutedBrush,
        });
        stack.Children.Add(_pickerMap);
        stack.Children.Add(_radiusLabel);
        stack.Children.Add(_radiusInput);
        stack.Children.Add(save);
        stack.Children.Add(cancel);
        _content.Content = new ScrollViewer
        {
            Content = stack,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };

        try
        {
            await _pickerMap.RefreshTilesAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Map unavailable: {ex.Message}");
        }
    }

    async Task SaveLocationAsync()
    {
        if (_nameInput is null || _radiusInput is null)
            return;
        if (string.IsNullOrWhiteSpace(_nameInput.Text))
        {
            SetStatus("Enter a display name.");
            return;
        }

        if (_coordinateInput is null
            || !TryParseCoordinate(_coordinateInput.Text, out var coordinate))
        {
            SetStatus("Enter a valid latitude, longitude coordinate.");
            return;
        }

        if (_effectiveFromInput is null
            || !DateTimeOffset.TryParse(
                _effectiveFromInput.Text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var effectiveFrom))
        {
            SetStatus("Enter a valid effective-from UTC timestamp.");
            return;
        }

        _selectedCoordinate = coordinate;
        var ssid = string.IsNullOrWhiteSpace(_ssidInput?.Text)
            ? null
            : new WifiEvidence(_ssidInput.Text.Trim());
        var policy = ssid is null
            ? PresencePolicyDefaults.LocationOnly
            : PresencePolicyDefaults.Standard;
        await _locations.SaveAsync(new TrackedLocation(
            _editingLocation?.Id ?? Guid.NewGuid(),
            _nameInput.Text.Trim(),
            new GeoCircle(coordinate, _radiusInput.Value),
            ssid,
            policy,
            effectiveFrom));
        _editingLocation = null;
        await PublishLedgerAsync();
        await ShowLocationsAsync();
    }

    async Task PublishLedgerAsync()
    {
        var publisher = _services.GetService<ILedgerFilePublisher>();
        if (publisher is null)
            return;

        try
        {
            var where = await publisher.PublishAsync();
            SetStatus($"Ledger saved to {where}");
        }
        catch (Exception ex)
        {
            SetStatus($"Could not publish ledger: {ex.Message}");
        }
    }

    async Task ShowHistoryAsync()
    {
        try
        {
            var locationHistory = await ReadAllAsync(_locations.ReadHistoryAsync());
            var events = await ReadAllAsync(_events.ReadAsync());
            var stack = PageStack(
                "History",
                "Confirmed transitions, with no continuous route or breadcrumb history.");

            foreach (var presenceEvent in events.OrderByDescending(item => item.At))
            {
                var name = ResolveLocationName(
                    locationHistory,
                    presenceEvent.LocationId,
                    presenceEvent.At);
                var label = presenceEvent.Transition == PresenceTransition.Arrived
                    ? "ARRIVED"
                    : "LEFT";
                var color = presenceEvent.Transition == PresenceTransition.Arrived
                    ? GraphicalProfile.AccentBrush
                    : GraphicalProfile.ActionBrush;
                stack.Children.Add(Card(
                    new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = label,
                                Classes = { "eyebrow" },
                                Foreground = color,
                            },
                            new TextBlock
                            {
                                Text = name,
                                FontSize = 18,
                                FontWeight = FontWeight.SemiBold,
                                Foreground = GraphicalProfile.TextBrush,
                            },
                            new TextBlock
                            {
                                Text = $"{presenceEvent.At.ToLocalTime():g} · "
                                    + $"{presenceEvent.Evidence.Confidence} evidence",
                                Foreground = GraphicalProfile.MutedBrush,
                            },
                        },
                    },
                    GraphicalProfile.SurfaceBrush));
            }

            if (events.Count == 0)
                stack.Children.Add(new TextBlock
                {
                    Text = "No presence events yet.",
                    Foreground = GraphicalProfile.MutedBrush,
                });
            _content.Content = new ScrollViewer
            {
                Content = stack,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                HorizontalScrollBarVisibility =
                    Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility =
                    Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            };
            SetStatus($"{events.Count} event{(events.Count == 1 ? string.Empty : "s")}");
        }
        catch (Exception ex)
        {
            _content.Content = ErrorSurface(
                "History is unavailable",
                "The local event ledger could not be read.",
                ex.Message,
                () => _ = ShowHistoryAsync());
            SetStatus($"Could not read history: {ex.Message}");
        }
    }

    static string ResolveLocationName(
        IEnumerable<TrackedLocation> history,
        Guid locationId,
        DateTimeOffset at)
    {
        var location = history
            .Where(item =>
                item.Id == locationId
                && (item.EffectiveFromUtc is null || item.EffectiveFromUtc <= at)
                && (item.EffectiveToUtc is null || at < item.EffectiveToUtc))
            .OrderBy(item => item.EffectiveFromUtc ?? DateTimeOffset.MinValue)
            .LastOrDefault();
        return location?.DisplayName ?? locationId.ToString();
    }

    async Task ShowDiagnosticsAsync()
    {
        var locations = await ReadAllAsync(_locations.ReadAsync());
        var states = await ReadAllAsync(_states.ReadAsync());
        var locationSource = _services.GetService<ILocationReadingSource>();
        var wifiSource = _services.GetService<IWifiObservationSource>();
        var coordinator = _services.GetService<PresenceObservationCoordinator>();
        var locationStatus = locationSource?.GetStatus();
        var wifiStatus = wifiSource?.GetStatus();
        var today = await ReadDayProjectionAsync(DateOnly.FromDateTime(DateTime.Now));
        var storage = _services.GetService<NdjsonPresenceStorage>();
        var stack = PageStack("Diagnostics", "Platform capability is reported separately from presence inference.");
        stack.Children.Add(DiagnosticLine(
            "Location source",
            locationStatus is { } locationCapability
                ? FormatSourceStatus(locationCapability)
                : "Unavailable on this host"));
        stack.Children.Add(DiagnosticLine(
            "Wi-Fi source",
            wifiStatus is { } wifiCapability
                ? FormatSourceStatus(wifiCapability)
                : "Unavailable on this host"));
        stack.Children.Add(DiagnosticLine("Locations monitored", locations.Count.ToString()));
        stack.Children.Add(DiagnosticLine(
            "Pending candidates",
            states.Count(state => state.State is PresenceState.CandidatePresent or PresenceState.CandidateAbsent)
                .ToString()));
        stack.Children.Add(DiagnosticLine(
            "Observation service",
            coordinator?.IsRunning == true ? "Active" : "Inactive"));
        stack.Children.Add(DiagnosticLine(
            "Last position evidence",
            coordinator?.LastPositionAt?.ToLocalTime().ToString("g") ?? "None"));
        stack.Children.Add(DiagnosticLine(
            "Last Wi-Fi evidence",
            coordinator?.LastWifiAt?.ToLocalTime().ToString("g") ?? "None"));
        stack.Children.Add(DiagnosticLine(
            "Last observation error",
            coordinator?.LastError?.Message ?? "None"));
        stack.Children.Add(DiagnosticLine(
            "Stored observations today",
            $"{today.Observations.Count} local sample{(today.Observations.Count == 1 ? string.Empty : "s")}"));
        AddLedgerFiles(stack, storage);
        _content.Content = new ScrollViewer { Content = stack };
        SetStatus("Diagnostics refreshed");
    }

    void AddLedgerFiles(StackPanel stack, NdjsonPresenceStorage? storage)
    {
        stack.Children.Add(new TextBlock
        {
            Text = "LEDGER FILES",
            Classes = { "eyebrow" },
            Foreground = GraphicalProfile.AccentBrush,
            Margin = new Thickness(0, 8, 0, 0),
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Every ledger file is copied to Downloads/PresenceLedger as it is written. Share sends the same files, including ones created later.",
            Foreground = GraphicalProfile.MutedBrush,
            TextWrapping = TextWrapping.Wrap,
        });

        var files = storage is null
            ? []
            : LedgerFiles.List(storage.RootDirectory);
        if (files.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "No ledger files yet.",
                Foreground = GraphicalProfile.TextBrush,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        else
        {
            foreach (var file in files)
            {
                stack.Children.Add(DiagnosticLine(
                    file.RelativePath,
                    FormatFileSize(file.LengthBytes)));
            }
        }

        var save = new Button
        {
            Content = "Save ledger to Downloads",
            Classes = { "primary-button" },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = GraphicalProfile.AccentFillBrush,
            Foreground = GraphicalProfile.TextBrush,
        };
        save.Click += async (_, _) =>
        {
            await PublishLedgerAsync();
            await ShowDiagnosticsAsync();
        };
        var share = new Button
        {
            Content = "Share ledger files",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 48,
            Background = GraphicalProfile.SurfaceBrush,
            Foreground = GraphicalProfile.TextBrush,
        };
        share.Click += async (_, _) =>
        {
            var sharer = _services.GetService<ILedgerFileShare>();
            if (sharer is null)
            {
                SetStatus("This host saves the ledger to Downloads instead of a share sheet.");
                await PublishLedgerAsync();
                return;
            }

            try
            {
                await PublishLedgerAsync();
                await sharer.ShareAsync();
                SetStatus("Ledger share opened");
            }
            catch (Exception ex)
            {
                SetStatus($"Could not share ledger: {ex.Message}");
            }
        };
        stack.Children.Add(save);
        stack.Children.Add(share);
    }

    static string FormatFileSize(long bytes) =>
        bytes >= 1024
            ? $"{bytes / 1024d:0.#} KB"
            : $"{bytes} B";

    void SetSelectedCoordinate(GeoCoordinate coordinate)
    {
        _selectedCoordinate = coordinate;
        if (_coordinateInput is not null)
            _coordinateInput.Text = FormatCoordinate(coordinate);
    }

    static string FormatCoordinate(GeoCoordinate coordinate) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{coordinate.Latitude:F6}, {coordinate.Longitude:F6}");

    static bool TryParseCoordinate(string? text, out GeoCoordinate coordinate)
    {
        coordinate = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var parts = text.Split(
            [',', ';', ' ', '\t', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2
            || !double.TryParse(
                parts[0],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var latitude)
            || !double.TryParse(
                parts[1],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var longitude))
            return false;

        try
        {
            coordinate = new GeoCoordinate(latitude, longitude);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    void UpdatePickerOverlays()
    {
        if (_pickerMap is null || _selectedCoordinate is not { } coordinate)
            return;

        _pickerMap.Markers =
        [
            new MapMarker("selected", coordinate, "Selected"),
        ];
        _pickerMap.Circles =
        [
            new MapCircleOverlay(
                "selected-radius",
                new GeoCircle(coordinate, _radiusInput?.Value ?? 200)),
        ];
        _pickerMap.SelectedCoordinate = coordinate;
    }

    StackPanel PageStack(string title, string subtitle) =>
        new()
        {
            Margin = new Thickness(18, 10, 18, 24),
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontSize = 26,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = GraphicalProfile.TextBrush,
                },
                new TextBlock
                {
                    Text = subtitle,
                    Foreground = GraphicalProfile.MutedBrush,
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };

    static Control DiagnosticLine(string label, string value) =>
        new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    Foreground = GraphicalProfile.MutedBrush,
                },
                new TextBlock
                {
                    Text = value,
                    Foreground = GraphicalProfile.TextBrush,
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };

    static string FormatSourceStatus(MobileSourceStatus status) =>
        string.IsNullOrWhiteSpace(status.Detail)
            ? status.Status.ToString()
            : $"{status.Status}: {status.Detail}";

    void SetStatus(string text) => _status.Text = text;

    static async Task<List<T>> ReadAllAsync<T>(IAsyncEnumerable<T> source)
    {
        var values = new List<T>();
        await foreach (var value in source)
            values.Add(value);
        return values;
    }
}
