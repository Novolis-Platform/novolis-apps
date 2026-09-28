using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Avalonia.Map;
using Novolis.Avalonia.Mobile;
using Novolis.Math.Geometry;
using PresenceLedger.App.Map;
using PresenceLedger.Core;

namespace PresenceLedger.App;

/// <summary>Small v1 shell for locations, setup, history, and diagnostics.</summary>
public sealed class MainView : UserControl
{
    static readonly GeoCoordinate DefaultMapCenter = new(58.14623, 7.99517);

    readonly ITrackedLocationStore _locations;
    readonly IPresenceEventStore _events;
    readonly IPresenceStateStore _states;
    readonly IMapTileSource _tileSource;
    readonly IMapSearchProvider _searchProvider;
    readonly IServiceProvider _services;
    readonly ContentControl _content = new();
    readonly TextBlock _status = new();

    GeoCoordinate? _selectedCoordinate;
    TextBox? _nameInput;
    TextBox? _ssidInput;
    Slider? _radiusInput;
    TextBlock? _radiusLabel;
    MapControl? _pickerMap;

    /// <summary>Creates the shared product view.</summary>
    public MainView(
        ITrackedLocationStore locations,
        IPresenceEventStore events,
        IPresenceStateStore states,
        IMapTileSource tileSource,
        IMapSearchProvider searchProvider,
        IServiceProvider services)
    {
        _locations = locations ?? throw new ArgumentNullException(nameof(locations));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _states = states ?? throw new ArgumentNullException(nameof(states));
        _tileSource = tileSource ?? throw new ArgumentNullException(nameof(tileSource));
        _searchProvider = searchProvider ?? throw new ArgumentNullException(nameof(searchProvider));
        _services = services ?? throw new ArgumentNullException(nameof(services));

        Background = new SolidColorBrush(Color.Parse("#0d1b2a"));
        BuildShell();
        AttachedToVisualTree += async (_, _) => await ShowLocationsAsync();
    }

    void BuildShell()
    {
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto,Auto"),
            Margin = new Thickness(16, 14, 16, 8),
        };
        var title = new TextBlock
        {
            Text = "Presence Ledger",
            FontSize = 24,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#dce9ef")),
            VerticalAlignment = VerticalAlignment.Center,
        };
        header.Children.Add(title);

        var locations = NavigationButton("Locations", async () => await ShowLocationsAsync());
        var history = NavigationButton("History", async () => await ShowHistoryAsync());
        var diagnostics = NavigationButton("Diagnostics", async () => await ShowDiagnosticsAsync());
        var add = NavigationButton("+ Add location", async () => await ShowAddLocationAsync());
        Grid.SetColumn(locations, 1);
        Grid.SetColumn(history, 2);
        Grid.SetColumn(diagnostics, 3);
        Grid.SetColumn(add, 4);
        header.Children.Add(locations);
        header.Children.Add(history);
        header.Children.Add(diagnostics);
        header.Children.Add(add);

        _status.Text = "Local only";
        _status.Foreground = new SolidColorBrush(Color.Parse("#91a9b5"));
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
            Margin = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(10, 7),
            Background = new SolidColorBrush(Color.Parse("#17384a")),
            Foreground = new SolidColorBrush(Color.Parse("#dce9ef")),
        };
        button.Click += async (_, _) => await action();
        return button;
    }

    async Task ShowLocationsAsync()
    {
        try
        {
            var locations = await ReadAllAsync(_locations.ReadAsync());
            var stack = PageStack("Locations", "Only semantic arrival and departure events are retained.");
            if (locations.Count == 0)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = "No locations configured yet.",
                    Foreground = MutedBrush,
                    Margin = new Thickness(0, 14),
                });
            }
            else
            {
                foreach (var location in locations)
                    stack.Children.Add(await BuildLocationCardAsync(location));
            }

            _content.Content = new ScrollViewer
            {
                Content = stack,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            };
            SetStatus($"{locations.Count} location{(locations.Count == 1 ? string.Empty : "s")} configured");
        }
        catch (Exception ex)
        {
            SetStatus($"Could not read locations: {ex.Message}");
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
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = location.DisplayName,
                    FontSize = 18,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = new SolidColorBrush(Color.Parse("#dce9ef")),
                },
                new TextBlock { Text = stateText, Foreground = MutedBrush },
                new TextBlock
                {
                    Text = $"{evidenceText} · {location.Area.RadiusMeters:0} m radius",
                    Foreground = MutedBrush,
                },
            },
        };
        return new Border
        {
            Child = content,
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 10),
            Background = new SolidColorBrush(Color.Parse("#142b3b")),
            BorderBrush = new SolidColorBrush(Color.Parse("#285268")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
        };
    }

    async Task ShowAddLocationAsync()
    {
        _selectedCoordinate = null;
        _nameInput = new TextBox { PlaceholderText = "Display name", MinWidth = 260 };
        _ssidInput = new TextBox { PlaceholderText = "Optional Wi-Fi SSID", MinWidth = 260 };
        _radiusInput = new Slider
        {
            Minimum = 50,
            Maximum = 1_000,
            Value = 200,
            Width = 300,
        };
        _radiusLabel = new TextBlock { Foreground = MutedBrush };
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
            Viewport = new MapViewport(DefaultMapCenter, 13),
            TileSource = _tileSource,
            Attribution = KartverketMap.Attribution,
            Height = 380,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        _pickerMap.PointSelected += coordinate =>
        {
            _selectedCoordinate = coordinate;
            UpdatePickerOverlays();
        };

        var searchInput = new TextBox { PlaceholderText = "Search an address (optional)", MinWidth = 280 };
        var searchResults = new StackPanel { Spacing = 4 };
        var searchButton = new Button { Content = "Search", Padding = new Thickness(12, 7) };
        searchButton.Click += async (_, _) =>
        {
            searchResults.Children.Clear();
            if (string.IsNullOrWhiteSpace(searchInput.Text))
                return;
            try
            {
                foreach (var result in await _searchProvider.SearchAsync(searchInput.Text))
                {
                    var resultButton = new Button
                    {
                        Content = result.DisplayName,
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                    };
                    resultButton.Click += (_, _) =>
                    {
                        _selectedCoordinate = result.Coordinate;
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
            Content = "Save location",
            Padding = new Thickness(14, 8),
            Background = new SolidColorBrush(Color.Parse("#b56f2b")),
            Foreground = Brushes.White,
        };
        save.Click += async (_, _) => await SaveLocationAsync();

        var stack = PageStack("Add location", "Choose a point and radius. Map content is used only during setup.");
        stack.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { _nameInput, _ssidInput },
        });
        stack.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { searchInput, searchButton },
        });
        stack.Children.Add(searchResults);
        stack.Children.Add(_pickerMap);
        stack.Children.Add(_radiusLabel);
        stack.Children.Add(_radiusInput);
        stack.Children.Add(save);
        _content.Content = new ScrollViewer { Content = stack };

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

        if (_selectedCoordinate is not { } coordinate)
        {
            SetStatus("Select a point on the map.");
            return;
        }

        var ssid = string.IsNullOrWhiteSpace(_ssidInput?.Text)
            ? null
            : new WifiEvidence(_ssidInput.Text.Trim());
        var policy = ssid is null
            ? PresencePolicyDefaults.LocationOnly
            : PresencePolicyDefaults.Standard;
        await _locations.SaveAsync(new TrackedLocation(
            Guid.NewGuid(),
            _nameInput.Text.Trim(),
            new GeoCircle(coordinate, _radiusInput.Value),
            ssid,
            policy));
        await ShowLocationsAsync();
    }

    async Task ShowHistoryAsync()
    {
        var locations = await ReadAllAsync(_locations.ReadAsync());
        var names = locations.ToDictionary(location => location.Id, location => location.DisplayName);
        var events = await ReadAllAsync(_events.ReadAsync());
        var stack = PageStack("History", "Semantic events only; no route or breadcrumb history.");

        foreach (var presenceEvent in events.OrderByDescending(item => item.At))
        {
            var name = names.TryGetValue(presenceEvent.LocationId, out var locationName)
                ? locationName
                : presenceEvent.LocationId.ToString();
            stack.Children.Add(new TextBlock
            {
                Text = $"{presenceEvent.At.ToLocalTime():g}  "
                    + $"{presenceEvent.Transition}  {name}",
                Foreground = new SolidColorBrush(Color.Parse("#dce9ef")),
                Margin = new Thickness(0, 3),
            });
        }

        if (events.Count == 0)
            stack.Children.Add(new TextBlock { Text = "No presence events yet.", Foreground = MutedBrush });
        _content.Content = new ScrollViewer { Content = stack };
        SetStatus($"{events.Count} event{(events.Count == 1 ? string.Empty : "s")}");
    }

    async Task ShowDiagnosticsAsync()
    {
        var locations = await ReadAllAsync(_locations.ReadAsync());
        var states = await ReadAllAsync(_states.ReadAsync());
        var locationSource = _services.GetService<ILocationReadingSource>();
        var wifiSource = _services.GetService<IWifiObservationSource>();
        var coordinator = _services.GetService<PresenceObservationCoordinator>();
        var stack = PageStack("Diagnostics", "Platform capability is reported separately from presence inference.");
        stack.Children.Add(DiagnosticLine(
            "Location source",
            locationSource?.GetStatus().Status.ToString() ?? "Unavailable on this host"));
        stack.Children.Add(DiagnosticLine(
            "Wi-Fi source",
            wifiSource?.GetStatus().Status.ToString() ?? "Unavailable on this host"));
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
        stack.Children.Add(DiagnosticLine("Stored observations", "0 — observations are ephemeral"));
        _content.Content = new ScrollViewer { Content = stack };
        SetStatus("Diagnostics refreshed");
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
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontSize = 26,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = new SolidColorBrush(Color.Parse("#dce9ef")),
                },
                new TextBlock { Text = subtitle, Foreground = MutedBrush },
            },
        };

    static Control DiagnosticLine(string label, string value) =>
        new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    Width = 190,
                    Foreground = MutedBrush,
                },
                new TextBlock
                {
                    Text = value,
                    Foreground = new SolidColorBrush(Color.Parse("#dce9ef")),
                },
            },
        };

    void SetStatus(string text) => _status.Text = text;

    static IBrush MutedBrush => new SolidColorBrush(Color.Parse("#91a9b5"));

    static async Task<List<T>> ReadAllAsync<T>(IAsyncEnumerable<T> source)
    {
        var values = new List<T>();
        await foreach (var value in source)
            values.Add(value);
        return values;
    }
}
