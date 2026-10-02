using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Avalonia.Map;
using Novolis.Math.Geometry;
using PresenceLedger.Core;

namespace PresenceLedger.App;

/// <summary>
/// Day map with one color per place and a dark-to-light sample gradient.
/// Playback scrubs that gradient along the day.
/// </summary>
sealed class DayMapSession
{
    const string PlayIcon = "M4,2 L4,14 L13,8 Z";
    const string PauseIcon = "M3,2 H6.5 V14 H3 Z M9.5,2 H13 V14 H9.5 Z";
    const string StepBackIcon = "M2,2 H5 V14 H2 Z M14,2 L6,8 L14,14 Z";
    const string StepForwardIcon = "M11,2 H14 V14 H11 Z M2,2 L10,8 L2,14 Z";

    static readonly Color[] PlaceInks =
    [
        GraphicalProfile.Accent,
        GraphicalProfile.Action,
        GraphicalProfile.AccentFill,
        GraphicalProfile.Danger,
    ];

    static readonly double[] Speeds = [1, 15, 60];

    readonly MapControl _map;
    readonly PresenceObservationDebugPoint[] _samples;
    readonly TrailPoint[] _path;
    readonly MapTrackOverlay[] _pathTracks;
    readonly List<MapTrackOverlay> _visibleTracks;
    readonly MapMarker[] _placeMarkers;
    readonly MapMarker[] _markers;
    readonly GeoCoordinate[] _headPoints = new GeoCoordinate[2];
    readonly GeoCoordinate[] _fitPoints;
    readonly Slider _scrub;
    readonly TextBlock _clock;
    readonly Button _play;
    readonly Button _speed;
    readonly DispatcherTimer _timer;
    readonly DateTimeOffset _start;
    readonly DateTimeOffset _end;
    int _speedIndex = 1;
    bool _applying;
    bool _fitApplied;
    DateTimeOffset _cursor;

    DayMapSession(
        IReadOnlyList<TrackedLocation> locations,
        IReadOnlyList<PresenceObservationDebugPoint> observations,
        IMapTileSource tileSource,
        string attribution,
        Func<MapControl, Task> refresh)
    {
        _samples = observations
            .Where(item => item.Position is not null)
            .OrderBy(item => item.At)
            .ToArray();
        _path = Decimate(_samples);
        _start = _samples.Length == 0 ? DateTimeOffset.UnixEpoch : _samples[0].At;
        _end = _samples.Length == 0 ? _start : _samples[^1].At;
        _cursor = _end;
        _pathTracks = new MapTrackOverlay[global::System.Math.Max(0, _path.Length - 1)];
        var center = locations.FirstOrDefault()?.Area.Center
            ?? _samples.FirstOrDefault()?.Position
            ?? new GeoCoordinate(58.14623, 7.99517);
        _placeMarkers = locations
            .Select((location, index) => new MapMarker(
                location.Id.ToString("N"),
                location.Area.Center,
                location.DisplayName,
                8,
                PlaceInk(index)))
            .ToArray();
        _markers = new MapMarker[_placeMarkers.Length + 1];
        _map = new MapControl
        {
            Viewport = new MapViewport(center, locations.Count == 0 ? 12 : 14),
            TileSource = tileSource,
            Attribution = attribution,
            MinHeight = 280,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            BackgroundBrush = GraphicalProfile.SurfaceBrush,
            Circles = locations
                .Select((location, index) => new MapCircleOverlay(
                    location.Id.ToString("N"),
                    location.Area,
                    location.DisplayName,
                    PlaceInk(index)))
                .ToArray(),
        };
        _fitPoints = locations
            .Select(location => location.Area.Center)
            .Concat(_path.Select(item => item.Position))
            .ToArray();
        _visibleTracks = new List<MapTrackOverlay>(_pathTracks.Length + 1);
        for (var index = 0; index < _pathTracks.Length; index++)
        {
            var ink = SampleInk(Fraction(_path[index + 1].At));
            _pathTracks[index] = new MapTrackOverlay(
                $"day-{index + 1}",
                [_path[index].Position, _path[index + 1].Position],
                null,
                ink,
                ink);
        }
        var span = global::System.Math.Max(1, (_end - _start).TotalSeconds);
        _clock = new TextBlock
        {
            Foreground = GraphicalProfile.TextBrush,
            FontSize = 16,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _scrub = new Slider
        {
            Minimum = 0,
            Maximum = span,
            Value = span,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 44,
            Margin = new Thickness(0, 6, 0, 0),
            IsEnabled = _end > _start,
            IsSnapToTickEnabled = false,
        };
        _scrub.PropertyChanged += (_, args) =>
        {
            if (args.Property != RangeBase.ValueProperty || _applying)
                return;

            _cursor = _start + TimeSpan.FromSeconds(_scrub.Value);
            Render();
        };
        _scrub.AddHandler(
            InputElement.PointerPressedEvent,
            (_, _) => StopPlayback(),
            RoutingStrategies.Tunnel);
        _play = IconButton(PlayIcon, "Play");
        _play.Click += (_, _) => TogglePlay();
        var back = IconButton(StepBackIcon, "Step back");
        back.Click += (_, _) => Step(-1);
        var forward = IconButton(StepForwardIcon, "Step forward");
        forward.Click += (_, _) => Step(1);
        _speed = TransportButton("15 min/s");
        _speed.MinWidth = 96;
        _speed.Click += (_, _) => CycleSpeed();
        ToolTip.SetTip(_speed, "Playback speed");
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => Advance();
        _map.SizeChanged += (_, args) =>
        {
            if (!_fitApplied
                && args.NewSize.Width > 0
                && args.NewSize.Height > 0)
            {
                _fitApplied = true;
                _map.FitToContent(_fitPoints);
            }
        };
        Render();

        var zoomIn = OverlayButton("+");
        zoomIn.Click += (_, _) => _map.ZoomIn();
        var zoomOut = OverlayButton("−");
        zoomOut.Click += (_, _) => _map.ZoomOut();
        var fit = OverlayButton("Fit");
        fit.Click += (_, _) => _map.FitToContent(_fitPoints);
        var refreshButton = OverlayButton("Refresh");
        refreshButton.Click += async (_, _) =>
        {
            try
            {
                await refresh(_map);
            }
            catch (Exception)
            {
                // The host reports tile failures.
            }
        };

        var iconRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition(GridLength.Auto),
            },
            Margin = new Thickness(0, 12, 0, 0),
        };
        iconRow.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { back, _play, forward },
        });
        var readout = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _speed, _clock },
        };
        iconRow.Children.Add(readout);
        Grid.SetColumn(readout, 2);

        var legend = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Margin = new Thickness(0, 4, 0, 0),
        };
        for (var index = 0; index < locations.Count; index++)
        {
            legend.Children.Add(new TextBlock
            {
                Text = locations[index].DisplayName,
                Foreground = new SolidColorBrush(PlaceInk(index)),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        var frame = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            RowDefinitions =
            {
                new RowDefinition(new GridLength(1, GridUnitType.Star)),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
            },
        };
        frame.Children.Add(new Grid
        {
            Children =
            {
                _map,
                new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 6,
                    Margin = new Thickness(10),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Children = { zoomIn, zoomOut, fit, refreshButton },
                },
            },
        });
        frame.Children.Add(iconRow);
        Grid.SetRow(iconRow, 1);
        frame.Children.Add(_scrub);
        Grid.SetRow(_scrub, 2);
        frame.Children.Add(legend);
        Grid.SetRow(legend, 3);
        frame.DetachedFromVisualTree += (_, _) => _timer.Stop();
        frame.AttachedToVisualTree += (_, _) =>
        {
            if (!_fitApplied)
                Dispatcher.UIThread.Post(
                    () =>
                    {
                        if (_map.Bounds.Width > 0 && _map.Bounds.Height > 0)
                        {
                            _fitApplied = true;
                            _map.FitToContent(_fitPoints);
                        }
                    },
                    DispatcherPriority.Loaded);
        };
        Surface = frame;
        Map = _map;
    }

    public Control Surface { get; }

    public MapControl Map { get; }

    public static DayMapSession Create(
        IReadOnlyList<TrackedLocation> locations,
        IReadOnlyList<PresenceObservationDebugPoint> observations,
        IMapTileSource tileSource,
        string attribution,
        Func<MapControl, Task> refresh) =>
        new(locations, observations, tileSource, attribution, refresh);

    static Color PlaceInk(int index) => PlaceInks[index % PlaceInks.Length];

    static Color SampleInk(double amount) =>
        MapInk.Lerp(
            MapInk.Lerp(GraphicalProfile.Background, GraphicalProfile.Accent, 0.5),
            GraphicalProfile.OnAccentFill,
            amount);

    void Render()
    {
        if (_samples.Length == 0)
        {
            _map.Markers = [];
            _visibleTracks.Clear();
            _map.Tracks = _visibleTracks;
            _map.RequestRender();
            _clock.Text = "No samples";
            return;
        }

        var amount = Fraction(_cursor);
        var visibleCount = UpperBound(_path, _cursor);
        var head = PositionAt(_cursor);
        _visibleTracks.Clear();
        for (var index = 0; index < visibleCount - 1; index++)
        {
            _visibleTracks.Add(_pathTracks[index]);
        }

        if (visibleCount > 0
            && Meters(_path[visibleCount - 1].Position, head) >= 1)
        {
            _headPoints[0] = _path[visibleCount - 1].Position;
            _headPoints[1] = head;
            var ink = SampleInk(amount);
            _visibleTracks.Add(new MapTrackOverlay(
                "day-head",
                _headPoints,
                null,
                ink,
                ink));
        }

        _placeMarkers.CopyTo(_markers, 0);
        _markers[^1] = new MapMarker("now", head, null, 10, SampleInk(amount));
        _map.Tracks = _visibleTracks;
        _map.Markers = _markers;
        _map.RequestRender();
        _clock.Text = _cursor.ToLocalTime().ToString("HH:mm:ss");
    }

    static int UpperBound(TrailPoint[] points, DateTimeOffset at)
    {
        var lower = 0;
        var upper = points.Length;
        while (lower < upper)
        {
            var middle = lower + ((upper - lower) / 2);
            if (points[middle].At <= at)
                lower = middle + 1;
            else
                upper = middle;
        }

        return lower;
    }

    void Step(int direction)
    {
        StopPlayback();
        if (_path.Length == 0)
            return;

        var target = direction > 0 ? _end : _start;
        if (direction > 0)
        {
            foreach (var point in _path)
            {
                if (point.At > _cursor.AddSeconds(0.5))
                {
                    target = point.At;
                    break;
                }
            }
        }
        else
        {
            foreach (var point in _path)
            {
                if (point.At < _cursor.AddSeconds(-0.5))
                    target = point.At;
                else
                    break;
            }
        }

        Seek(target);
    }

    void TogglePlay()
    {
        if (_timer.IsEnabled)
        {
            StopPlayback();
            return;
        }

        if (_end <= _start)
            return;

        if (_cursor >= _end - TimeSpan.FromSeconds(1))
            _cursor = _start;

        _play.Content = Glyph(PauseIcon);
        Seek(_cursor);
        _timer.Start();
    }

    void StopPlayback()
    {
        _timer.Stop();
        _play.Content = Glyph(PlayIcon);
    }

    void CycleSpeed()
    {
        _speedIndex = (_speedIndex + 1) % Speeds.Length;
        _speed.Content = $"{Speeds[_speedIndex]:0} min/s";
    }

    void Advance()
    {
        if (_end <= _start)
        {
            StopPlayback();
            return;
        }

        var next = _cursor + TimeSpan.FromSeconds(Speeds[_speedIndex] * 6);
        if (next >= _end)
        {
            Seek(_end);
            StopPlayback();
            return;
        }

        Seek(next);
    }

    void Seek(DateTimeOffset at)
    {
        _cursor = at < _start ? _start : at > _end ? _end : at;
        _applying = true;
        _scrub.Value = (_cursor - _start).TotalSeconds;
        _applying = false;
        Render();
    }

    double Fraction(DateTimeOffset at)
    {
        var span = (_end - _start).TotalSeconds;
        if (span <= 0)
            return 1;

        return global::System.Math.Clamp((at - _start).TotalSeconds / span, 0, 1);
    }

    GeoCoordinate PositionAt(DateTimeOffset at)
    {
        if (at <= _samples[0].At)
            return _samples[0].Position!.Value;

        if (at >= _samples[^1].At)
            return _samples[^1].Position!.Value;

        var lower = 0;
        var upper = _samples.Length - 1;
        while (lower + 1 < upper)
        {
            var mid = (lower + upper) / 2;
            if (_samples[mid].At <= at)
                lower = mid;
            else
                upper = mid;
        }

        var from = _samples[lower];
        var to = _samples[upper];
        var span = (to.At - from.At).TotalSeconds;
        var amount = span <= 0 ? 1 : (at - from.At).TotalSeconds / span;
        var origin = from.Position!.Value;
        var destination = to.Position!.Value;
        var longitudeDelta = destination.Longitude - origin.Longitude;
        if (longitudeDelta > 180)
            longitudeDelta -= 360;
        else if (longitudeDelta < -180)
            longitudeDelta += 360;

        var longitude = origin.Longitude + (longitudeDelta * amount);
        if (longitude > 180)
            longitude -= 360;
        else if (longitude < -180)
            longitude += 360;

        return new GeoCoordinate(
            origin.Latitude + ((destination.Latitude - origin.Latitude) * amount),
            longitude);
    }

    static TrailPoint[] Decimate(PresenceObservationDebugPoint[] samples)
    {
        if (samples.Length == 0)
            return [];

        var kept = new List<TrailPoint>(samples.Length);
        foreach (var sample in samples)
        {
            var position = sample.Position!.Value;
            if (kept.Count == 0 || Meters(kept[^1].Position, position) >= 20)
                kept.Add(new TrailPoint(sample.At, position));
        }

        var last = samples[^1];
        if (kept[^1].At != last.At)
            kept.Add(new TrailPoint(last.At, last.Position!.Value));

        return kept.ToArray();
    }

    static double Meters(GeoCoordinate origin, GeoCoordinate destination)
    {
        const double metersPerDegree = 111_320;
        var latitude = (origin.Latitude + destination.Latitude) * 0.5 * (global::System.Math.PI / 180);
        var longitudeDelta = destination.Longitude - origin.Longitude;
        if (longitudeDelta > 180)
            longitudeDelta -= 360;
        else if (longitudeDelta < -180)
            longitudeDelta += 360;
        var east = longitudeDelta * metersPerDegree * global::System.Math.Cos(latitude);
        var north = (destination.Latitude - origin.Latitude) * metersPerDegree;
        return global::System.Math.Sqrt((east * east) + (north * north));
    }

    static Control Glyph(string data) =>
        new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(data),
            Fill = GraphicalProfile.TextBrush,
            Stretch = Stretch.Uniform,
            Width = 18,
            Height = 18,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

    static Button IconButton(string data, string tip)
    {
        var button = TransportButton(Glyph(data));
        button.Width = 48;
        button.Padding = new Thickness(0);
        ToolTip.SetTip(button, tip);
        return button;
    }

    static Button TransportButton(object content) =>
        new()
        {
            Content = content,
            MinWidth = 48,
            MinHeight = 48,
            Padding = new Thickness(12, 8),
            Background = GraphicalProfile.SurfaceBrush,
            Foreground = GraphicalProfile.TextBrush,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

    static Button OverlayButton(string label) =>
        new()
        {
            Content = label,
            MinWidth = 44,
            MinHeight = 44,
            Background = GraphicalProfile.SurfaceBrush,
            Foreground = GraphicalProfile.TextBrush,
        };

    readonly record struct TrailPoint(DateTimeOffset At, GeoCoordinate Position);
}
