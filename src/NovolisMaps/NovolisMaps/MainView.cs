using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Avalonia.Map;
using Novolis.IO.Maps;
using Novolis.Math.Geometry;

namespace NovolisMaps;

/// <summary>Shared map explorer surface for the Windows and Android heads.</summary>
internal sealed class MainView : UserControl
{
    static readonly GeoCoordinate DefaultCenter = new(58.14623, 7.99517);

    readonly MapSourceCatalog _catalog;
    readonly MapPinStore _pinStore;
    readonly MapControl _map = new();
    readonly ComboBox _providerPicker = new();
    readonly TextBox _searchInput = new();
    readonly StackPanel _searchResults = new();
    readonly TextBlock _selectionText = new();
    readonly TextBlock _measurementText = new();
    readonly TextBox _pinLabel = new();
    readonly Button _savePin = new();
    readonly Button _copyCoordinate = new();
    readonly Button _finishDrawing = new();
    readonly Button _cancelDrawing = new();
    readonly StackPanel _pinsPanel = new();
    readonly TextBlock _status = new();
    readonly List<MapPin> _pins = [];

    MapProviderDefinition _provider;
    GeoCoordinate? _selectedCoordinate;
    string? _selectedPlaceName;
    GeoDrawing? _measurement;
    CancellationTokenSource? _searchCancellation;
    bool _hasLoaded;

    /// <summary>Creates the map explorer from the shared map and pin services.</summary>
    public MainView(MapSourceCatalog catalog, MapPinStore pinStore)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _pinStore = pinStore ?? throw new ArgumentNullException(nameof(pinStore));
        _provider = _catalog.Default;

        Background = GraphicalProfile.BackgroundBrush;
        ConfigureMap();
        BuildShell();
        UpdateSelectionSurface();
        RefreshPins();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    void ConfigureMap()
    {
        _map.Viewport = new MapViewport(DefaultCenter, 6);
        _map.TileSource = _provider.TileSource;
        _map.Attribution = _provider.RasterSource.Template.Attribution;
        _map.MinHeight = 320;
        _map.HorizontalAlignment = HorizontalAlignment.Stretch;
        _map.VerticalAlignment = VerticalAlignment.Stretch;
        _map.BackgroundBrush = GraphicalProfile.SurfaceBrush;
        _map.InteractionOptions = new MapInteractionOptions
        {
            EnableClipboardShortcuts = true,
            EnableKeyboardNavigation = true,
            EnableDrawing = true,
            ShowMeasurementResults = true,
        };
        _map.PointSelected += OnPointSelected;
        _map.MarkerSelected += OnMarkerSelected;
        _map.DrawingCompleted += OnDrawingCompleted;
    }

    void BuildShell()
    {
        _status.Text = "Local pins · provider imagery · no account required";
        _status.Foreground = GraphicalProfile.MutedBrush;
        _status.FontSize = 11;
        _status.Margin = new Thickness(0, 6, 0, 0);

        var header = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = "MAP EXPLORER",
                    Classes = { "eyebrow" },
                    Foreground = GraphicalProfile.AccentBrush,
                },
                new TextBlock
                {
                    Text = "Novolis Maps",
                    Classes = { "page-title" },
                    Foreground = GraphicalProfile.TextBrush,
                },
                new TextBlock
                {
                    Text = "Search a place, save a pin, or measure a route without sending your map to an account.",
                    Classes = { "body-copy" },
                    Foreground = GraphicalProfile.MutedBrush,
                },
            },
        };

        var search = BuildSearchSurface();
        var tools = BuildToolSurface();
        var details = BuildDetailsSurface();

        var shell = new DockPanel
        {
            Margin = new Thickness(16, 12, 16, 12),
            LastChildFill = true,
        };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(search, Dock.Top);
        DockPanel.SetDock(tools, Dock.Bottom);
        DockPanel.SetDock(details, Dock.Bottom);
        shell.Children.Add(header);
        shell.Children.Add(search);
        shell.Children.Add(tools);
        shell.Children.Add(details);
        shell.Children.Add(BuildMapSurface());
        Content = shell;
    }

    Control BuildSearchSurface()
    {
        var searchButton = new Button
        {
            Content = "Search",
            Classes = { "primary-button" },
            Background = GraphicalProfile.AccentFillBrush,
            Foreground = GraphicalProfile.TextBrush,
            MinWidth = 92,
        };
        searchButton.Click += async (_, _) => await SearchAsync();
        _searchInput.PlaceholderText = "Search an address or place";
        _searchInput.MinHeight = 44;
        _searchInput.HorizontalAlignment = HorizontalAlignment.Stretch;
        _searchInput.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Enter)
                return;

            args.Handled = true;
            _ = SearchAsync();
        };

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 8,
            Margin = new Thickness(0, 12, 0, 0),
        };
        row.Children.Add(_searchInput);
        row.Children.Add(searchButton);
        Grid.SetColumn(searchButton, 1);

        _searchResults.Spacing = 4;
        return new StackPanel
        {
            Spacing = 6,
            Children =
            {
                row,
                new ScrollViewer
                {
                    Content = _searchResults,
                    MaxHeight = 150,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                },
            },
        };
    }

    Control BuildToolSurface()
    {
        _providerPicker.ItemsSource = _catalog.Providers;
        _providerPicker.SelectedIndex = 0;
        _providerPicker.MinWidth = 150;
        _providerPicker.MinHeight = 42;
        ToolTip.SetTip(_providerPicker, _provider.Description);
        _providerPicker.SelectionChanged += (_, _) =>
        {
            if (_providerPicker.SelectedItem is MapProviderDefinition provider)
                _ = SelectProviderAsync(provider);
        };

        var fit = ToolButton("Fit", () => FitContent());
        var route = ToolButton("Measure route", () => StartDrawing(GeoDrawingKind.Polyline));
        var area = ToolButton("Measure area", () => StartDrawing(GeoDrawingKind.Polygon));
        _finishDrawing.Content = "Finish";
        _finishDrawing.MinHeight = 42;
        _finishDrawing.Click += (_, _) => FinishDrawing();
        _finishDrawing.IsVisible = false;
        _cancelDrawing.Content = "Cancel";
        _cancelDrawing.MinHeight = 42;
        _cancelDrawing.Click += (_, _) => CancelDrawing();
        _cancelDrawing.IsVisible = false;

        var tools = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = "Style",
                    Foreground = GraphicalProfile.MutedBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 2, 0),
                },
                _providerPicker,
                fit,
                route,
                area,
                _finishDrawing,
                _cancelDrawing,
            },
        };
        return new ScrollViewer
        {
            Content = tools,
            Margin = new Thickness(0, 8, 0, 8),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
    }

    Control BuildMapSurface()
    {
        var zoomIn = MapOverlayButton("+", "Zoom in", _map.ZoomIn);
        var zoomOut = MapOverlayButton("−", "Zoom out", _map.ZoomOut);
        var fit = MapOverlayButton("Fit", "Fit saved pins", FitContent);
        var refresh = MapOverlayButton(
            "↻",
            "Refresh map tiles",
            () => _ = RefreshTilesAsync());
        var overlay = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(10),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Children = { zoomIn, zoomOut, fit, refresh },
        };
        return new Grid
        {
            MinHeight = 320,
            Children =
            {
                _map,
                overlay,
            },
        };
    }

    Control BuildDetailsSurface()
    {
        _selectionText.Foreground = GraphicalProfile.TextBrush;
        _selectionText.TextWrapping = TextWrapping.Wrap;
        _selectionText.Text = "Tap the map or choose a search result to select a coordinate.";

        _pinLabel.PlaceholderText = "Pin name (optional)";
        _pinLabel.MinHeight = 42;
        _pinLabel.HorizontalAlignment = HorizontalAlignment.Stretch;

        _savePin.Content = "Save pin";
        _savePin.MinHeight = 42;
        _savePin.Background = GraphicalProfile.ActionBrush;
        _savePin.Foreground = Brushes.White;
        _savePin.Click += async (_, _) => await SavePinAsync();

        _copyCoordinate.Content = "Copy";
        _copyCoordinate.MinHeight = 42;
        _copyCoordinate.Click += async (_, _) => await CopyCoordinateAsync();

        _measurementText.Foreground = GraphicalProfile.MutedBrush;
        _measurementText.TextWrapping = TextWrapping.Wrap;
        _measurementText.Text = "No measurement yet.";

        var pinActions = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 6,
            Children =
            {
                _pinLabel,
                _savePin,
                _copyCoordinate,
            },
        };
        Grid.SetColumn(_savePin, 1);
        Grid.SetColumn(_copyCoordinate, 2);

        _pinsPanel.Spacing = 6;
        var details = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = "SELECTED PLACE",
                    Classes = { "eyebrow" },
                    Foreground = GraphicalProfile.AccentBrush,
                },
                _selectionText,
                pinActions,
                _measurementText,
                new TextBlock
                {
                    Text = "SAVED PINS",
                    Classes = { "eyebrow" },
                    Foreground = GraphicalProfile.AccentBrush,
                    Margin = new Thickness(0, 6, 0, 0),
                },
                _pinsPanel,
                _status,
            },
        };
        return new Border
        {
            Child = new ScrollViewer
            {
                Content = details,
                MaxHeight = 250,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            },
            Padding = new Thickness(12),
            Background = GraphicalProfile.SurfaceBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Margin = new Thickness(0, 8, 0, 8),
        };
    }

    static Button ToolButton(string label, Action action)
    {
        var button = new Button
        {
            Content = label,
            MinHeight = 42,
            Padding = new Thickness(12, 7),
        };
        button.Click += (_, _) => action();
        return button;
    }

    static Button MapOverlayButton(string label, string tip, Action action)
    {
        var button = new Button
        {
            Content = label,
            MinWidth = 44,
            MinHeight = 42,
            Padding = new Thickness(9, 6),
            Background = GraphicalProfile.SurfaceBrush,
            Foreground = GraphicalProfile.TextBrush,
        };
        ToolTip.SetTip(button, tip);
        button.Click += (_, _) => action();
        return button;
    }

    async void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs args)
    {
        if (_hasLoaded)
            return;

        _hasLoaded = true;
        try
        {
            _pins.AddRange(await _pinStore.ReadAsync());
            RefreshPins();
            RefreshMap();
            await RefreshTilesAsync();
            SetStatus($"{_provider.DisplayName} · map ready");
        }
        catch (Exception exception)
        {
            SetStatus($"Could not load saved map state: {exception.Message}");
        }
    }

    void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs args)
    {
        _searchCancellation?.Cancel();
        _searchCancellation = null;
    }

    void OnPointSelected(GeoCoordinate coordinate)
    {
        _selectedCoordinate = coordinate;
        _selectedPlaceName = null;
        _pinLabel.Text = string.Empty;
        UpdateSelectionSurface();
        RefreshMap();
        SetStatus("Coordinate selected");
    }

    void OnMarkerSelected(MapMarker marker)
    {
        if (!Guid.TryParse(marker.Id, out var id))
            return;

        var pin = _pins.FirstOrDefault(item => item.Id == id);
        if (pin is null)
            return;

        _selectedCoordinate = pin.Coordinate;
        _selectedPlaceName = pin.Label;
        _pinLabel.Text = pin.Label;
        UpdateSelectionSurface();
        SetStatus($"Selected {pin.Label}");
    }

    void OnDrawingCompleted(GeoDrawing drawing)
    {
        _measurement = drawing;
        _finishDrawing.IsVisible = false;
        _cancelDrawing.IsVisible = false;
        RenderMeasurement();
        SetStatus($"{drawing.Kind} measurement completed");
    }

    async Task SearchAsync()
    {
        var query = _searchInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            SetStatus("Enter an address or place to search.");
            return;
        }

        _searchCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _searchCancellation = cancellation;
        _searchResults.Children.Clear();
        _searchResults.Children.Add(new TextBlock
        {
            Text = $"Searching with {_provider.DisplayName}…",
            Foreground = GraphicalProfile.MutedBrush,
        });
        try
        {
            var results = await _provider.Search.SearchAsync(query, cancellation.Token);
            if (!ReferenceEquals(_searchCancellation, cancellation))
                return;

            _searchResults.Children.Clear();
            if (results.Count == 0)
            {
                _searchResults.Children.Add(new TextBlock
                {
                    Text = "No places found.",
                    Foreground = GraphicalProfile.MutedBrush,
                });
                return;
            }

            foreach (var result in results)
            {
                var resultButton = new Button
                {
                    Content = result.DisplayName,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    MinHeight = 42,
                };
                resultButton.Click += (_, _) => SelectSearchResult(result);
                _searchResults.Children.Add(resultButton);
            }

            SetStatus($"{results.Count} result{(results.Count == 1 ? string.Empty : "s")} found");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _searchResults.Children.Clear();
            _searchResults.Children.Add(new TextBlock
            {
                Text = "Search is unavailable while offline.",
                Foreground = GraphicalProfile.DangerBrush,
            });
            SetStatus($"Search failed: {exception.Message}");
        }
        finally
        {
            if (ReferenceEquals(_searchCancellation, cancellation))
                _searchCancellation = null;
        }
    }

    void SelectSearchResult(MapPlace result)
    {
        _selectedCoordinate = result.Coordinate;
        _selectedPlaceName = result.DisplayName;
        _pinLabel.Text = result.DisplayName;
        _searchResults.Children.Clear();
        _map.SetViewport(result.Coordinate, 15);
        _map.SelectedCoordinate = result.Coordinate;
        UpdateSelectionSurface();
        RefreshMap();
        SetStatus($"Centered on {result.DisplayName}");
    }

    async Task SelectProviderAsync(MapProviderDefinition provider)
    {
        if (ReferenceEquals(provider, _provider))
            return;

        _provider = provider;
        _map.TileSource = provider.TileSource;
        _map.Attribution = provider.RasterSource.Template.Attribution;
        ToolTip.SetTip(_providerPicker, provider.Description);
        _searchResults.Children.Clear();
        SetStatus($"Loading {provider.DisplayName}…");
        await RefreshTilesAsync();
    }

    async Task RefreshTilesAsync()
    {
        try
        {
            await _map.RefreshTilesAsync();
            SetStatus(
                _map.ErrorMessage
                ?? (_map.HasStaleTiles
                    ? $"{_provider.DisplayName} · cached tiles"
                    : $"{_provider.DisplayName} · map refreshed"));
        }
        catch (Exception exception)
        {
            SetStatus($"Map unavailable: {exception.Message}");
        }
    }

    void FitContent()
    {
        var points = _pins.Select(pin => pin.Coordinate).ToList();
        if (_selectedCoordinate is { } selected)
            points.Add(selected);
        if (points.Count == 0)
        {
            _map.SetViewport(DefaultCenter, 6);
            SetStatus("Returned to the opening view");
            return;
        }

        _map.FitToContent(points);
        SetStatus("Map fitted to saved content");
    }

    void StartDrawing(GeoDrawingKind kind)
    {
        _measurement = null;
        RenderMeasurement();
        if (!_map.BeginDrawing(kind))
        {
            SetStatus("Drawing is unavailable on this map.");
            return;
        }

        _finishDrawing.IsVisible = true;
        _cancelDrawing.IsVisible = true;
        SetStatus(
            kind == GeoDrawingKind.Polyline
                ? "Tap or click points for the route, then choose Finish."
                : "Tap or click at least three points for the area, then choose Finish.");
    }

    void FinishDrawing()
    {
        if (_map.CompleteDrawing() is null)
            SetStatus("Add more points before finishing this measurement.");
    }

    void CancelDrawing()
    {
        _map.CancelDrawing();
        _measurement = null;
        _finishDrawing.IsVisible = false;
        _cancelDrawing.IsVisible = false;
        RenderMeasurement();
        SetStatus("Measurement canceled");
    }

    async Task SavePinAsync()
    {
        if (_selectedCoordinate is not { } coordinate)
        {
            SetStatus("Select a point before saving a pin.");
            return;
        }

        var label = _pinLabel.Text?.Trim();
        if (string.IsNullOrWhiteSpace(label))
            label = _selectedPlaceName ?? $"Pin {_pins.Count + 1}";

        var existingIndex = _pins.FindIndex(pin => pin.Coordinate == coordinate);
        if (existingIndex >= 0)
            _pins[existingIndex] = _pins[existingIndex] with { Label = label };
        else
            _pins.Add(new MapPin(Guid.NewGuid(), label, coordinate));

        try
        {
            await _pinStore.WriteAsync(_pins);
            _selectedPlaceName = label;
            RefreshPins();
            RefreshMap();
            SetStatus($"Saved {label}");
        }
        catch (Exception exception)
        {
            SetStatus($"Could not save pin: {exception.Message}");
        }
    }

    async Task RemovePinAsync(MapPin pin)
    {
        _pins.Remove(pin);
        if (_selectedCoordinate == pin.Coordinate)
        {
            _selectedCoordinate = null;
            _selectedPlaceName = null;
            _map.ClearSelection();
            UpdateSelectionSurface();
        }

        try
        {
            await _pinStore.WriteAsync(_pins);
            RefreshPins();
            RefreshMap();
            SetStatus($"Removed {pin.Label}");
        }
        catch (Exception exception)
        {
            SetStatus($"Could not remove pin: {exception.Message}");
        }
    }

    void FocusPin(MapPin pin)
    {
        _selectedCoordinate = pin.Coordinate;
        _selectedPlaceName = pin.Label;
        _pinLabel.Text = pin.Label;
        _map.SetViewport(pin.Coordinate, 15);
        RefreshMap();
        if (_map.Markers?.FirstOrDefault(marker => marker.Id == pin.Id.ToString("N"))
            is { } marker)
        {
            _map.SelectMarker(marker);
        }

        UpdateSelectionSurface();
        SetStatus($"Centered on {pin.Label}");
    }

    async Task CopyCoordinateAsync()
    {
        if (_selectedCoordinate is null)
        {
            SetStatus("Select a point before copying coordinates.");
            return;
        }

        if (await _map.CopySelectedCoordinateAsync())
            SetStatus("Coordinate copied");
        else
            SetStatus("Clipboard is unavailable.");
    }

    void RefreshMap()
    {
        var markers = _pins
            .Select(
                (pin, index) => new MapMarker(
                    pin.Id.ToString("N"),
                    pin.Coordinate,
                    pin.Label,
                    9,
                    PinInk(index)))
            .ToList();
        if (_selectedCoordinate is { } selected
            && !_pins.Any(pin => pin.Coordinate == selected))
        {
            markers.Add(new MapMarker(
                "selected",
                selected,
                _selectedPlaceName ?? "Selected point",
                10,
                GraphicalProfile.Action));
        }

        _map.Markers = markers;
        _map.SelectedCoordinate = _selectedCoordinate;
        RenderDrawing();
    }

    void RefreshPins()
    {
        _pinsPanel.Children.Clear();
        if (_pins.Count == 0)
        {
            _pinsPanel.Children.Add(new TextBlock
            {
                Text = "No saved pins yet.",
                Foreground = GraphicalProfile.MutedBrush,
            });
            return;
        }

        foreach (var pin in _pins.OrderBy(item => item.Label, StringComparer.CurrentCulture))
            _pinsPanel.Children.Add(BuildPinRow(pin));
    }

    Control BuildPinRow(MapPin pin)
    {
        var show = new Button
        {
            Content = "Show",
            MinHeight = 38,
            Padding = new Thickness(10, 6),
        };
        show.Click += (_, _) => FocusPin(pin);
        var remove = new Button
        {
            Content = "Remove",
            MinHeight = 38,
            Padding = new Thickness(10, 6),
        };
        remove.Click += async (_, _) => await RemovePinAsync(pin);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children = { show, remove },
        };
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 8,
            Children =
            {
                new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = pin.Label,
                            FontWeight = FontWeight.SemiBold,
                            Foreground = GraphicalProfile.TextBrush,
                        },
                        new TextBlock
                        {
                            Text = GeoCoordinateText.Format(pin.Coordinate),
                            Foreground = GraphicalProfile.MutedBrush,
                        },
                    },
                },
                actions,
            },
        };
        Grid.SetColumn(actions, 1);
        return new Border
        {
            Child = row,
            Padding = new Thickness(8),
            Background = GraphicalProfile.RaisedBrush,
            CornerRadius = new CornerRadius(8),
        };
    }

    void UpdateSelectionSurface()
    {
        if (_selectedCoordinate is not { } coordinate)
        {
            _selectionText.Text = "Tap the map or choose a search result to select a coordinate.";
            _savePin.IsEnabled = false;
            _copyCoordinate.IsEnabled = false;
            return;
        }

        var label = _selectedPlaceName is null
            ? "Selected coordinate"
            : _selectedPlaceName;
        _selectionText.Text = $"{label}\n{GeoCoordinateText.Format(coordinate)}";
        _savePin.IsEnabled = true;
        _copyCoordinate.IsEnabled = true;
    }

    void RenderDrawing()
    {
        if (_measurement is not { } drawing)
        {
            _map.Tracks = [];
            _map.Polygons = [];
            _map.Circles = [];
            return;
        }

        var ink = GraphicalProfile.Action;
        switch (drawing.Kind)
        {
            case GeoDrawingKind.Polyline:
                _map.Tracks =
                [
                    new MapTrackOverlay(
                        "measurement",
                        drawing.Points,
                        "Measurement",
                        ink,
                        GraphicalProfile.Accent),
                ];
                _map.Polygons = [];
                _map.Circles = [];
                break;
            case GeoDrawingKind.Polygon:
                _map.Polygons =
                [
                    new MapPolygonOverlay(
                        "measurement",
                        drawing.ClosedPoints,
                        "Measurement",
                        ink,
                        GraphicalProfile.AccentFill),
                ];
                _map.Tracks = [];
                _map.Circles = [];
                break;
            default:
                _map.Tracks = [];
                _map.Polygons = [];
                _map.Circles = [];
                break;
        }

        _map.RequestRender();
    }

    void RenderMeasurement()
    {
        if (_measurement is not { } drawing)
        {
            _measurementText.Text = "No measurement yet.";
            RenderDrawing();
            return;
        }

        var statistics = drawing.Statistics;
        _measurementText.Text = drawing.Kind == GeoDrawingKind.Polygon
            ? $"Area measurement · {FormatDistance(statistics.LengthMeters)} perimeter · {FormatArea(statistics.AreaSquareMeters)}"
            : $"Route measurement · {FormatDistance(statistics.LengthMeters)}";
        RenderDrawing();
    }

    static string FormatDistance(double meters) =>
        meters < 1_000
            ? $"{meters:0} m"
            : $"{meters / 1_000:0.00} km";

    static string FormatArea(double squareMeters) =>
        squareMeters < 10_000
            ? $"{squareMeters:0} m²"
            : $"{squareMeters / 1_000_000:0.00} km²";

    static Color PinInk(int index) =>
        (index % 3) switch
        {
            0 => GraphicalProfile.Accent,
            1 => GraphicalProfile.Action,
            _ => GraphicalProfile.AccentFill,
        };

    void SetStatus(string text) => _status.Text = text;
}
