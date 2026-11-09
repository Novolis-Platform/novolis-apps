using Novolis.Avalonia.GraphicalProfile;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PresenceLedger.Core;

namespace PresenceLedger.App;

/// <summary>Builds the focused local day journal surface.</summary>
internal static class PresenceTodayView
{
    public static Control Build(
        PresenceDayProjection day,
        IReadOnlyList<TrackedLocation> locations,
        DateTimeOffset now,
        ObservationSnapshot observation,
        Action previousDay,
        Action nextDay,
        Action today,
        Action openMap,
        Action addLocation,
        Action openSettings,
        Action openReport)
    {
        var content = LedgerChrome.Page(
            "YOUR DAY",
            FormatDate(day.DisplayDate),
            "A private, explainable record of where your configured locations saw you.");

        content.Children.Add(BuildObservationCard(observation, openSettings));
        content.Children.Add(BuildDateBar(day.DisplayDate, previousDay, nextDay, today));

        var current = day.Intervals
            .Where(interval =>
                interval.StartedAt <= now
                && (interval.EndedAt is null || now < interval.EndedAt))
            .OrderByDescending(interval => interval.StartedAt)
            .FirstOrDefault();
        content.Children.Add(BuildHero(current, day, locations));

        var confirmedMinutes = day.Intervals.Sum(interval =>
        {
            var end = interval.EndedAt ?? now;
            return Math.Max(0, (end - interval.StartedAt).TotalMinutes);
        });
        content.Children.Add(LedgerChrome.MetricsRow(
            ("Segments", day.Intervals.Count.ToString()),
            ("Samples", day.Observations.Count.ToString()),
            ("Present", FormatDuration(TimeSpan.FromMinutes(confirmedMinutes)))));

        var actions = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 8,
        };
        var mapButton = LedgerChrome.Primary("Open day map", openMap);
        var reportButton = LedgerChrome.Secondary("Open report", openReport);
        Grid.SetColumn(mapButton, 0);
        Grid.SetColumn(reportButton, 1);
        actions.Children.Add(mapButton);
        actions.Children.Add(reportButton);
        content.Children.Add(actions);

        var timelineHeader = new DockPanel();
        var timelineTitle = LedgerChrome.Eyebrow("TIMELINE");
        DockPanel.SetDock(timelineTitle, Dock.Left);
        timelineHeader.Children.Add(timelineTitle);
        var count = new TextBlock
        {
            Text = $"{day.Intervals.Count} segment{(day.Intervals.Count == 1 ? string.Empty : "s")}",
            Foreground = GraphicalProfile.MutedBrush,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        DockPanel.SetDock(count, Dock.Right);
        timelineHeader.Children.Add(count);
        content.Children.Add(timelineHeader);

        if (day.Intervals.Count == 0)
        {
            content.Children.Add(locations.Count == 0
                ? BuildEmptyState(day, addLocation)
                : BuildWatchingState(locations));
        }
        else
        {
            foreach (var interval in day.Intervals)
                content.Children.Add(BuildInterval(interval, day.DisplayTimeZone));
        }

        content.Children.Add(BuildEvidenceSummary(day, locations));

        return new ScrollViewer
        {
            Content = content,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            HorizontalScrollBarVisibility =
                Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility =
                Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };
    }

    static Control BuildObservationCard(ObservationSnapshot observation, Action openSettings)
    {
        var chips = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
        };
        chips.Children.Add(LedgerChrome.Chip(
            observation.IsRunning ? "OBSERVER ON" : "OBSERVER OFF",
            accent: observation.IsRunning));
        chips.Children.Add(LedgerChrome.Chip(observation.PollLabel));
        chips.Children.Add(LedgerChrome.Chip(observation.PublishLabel));

        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(chips);
        stack.Children.Add(LedgerChrome.Muted(observation.Summary));
        if (!string.IsNullOrWhiteSpace(observation.LastPlacement))
        {
            stack.Children.Add(LedgerChrome.Body(
                $"Placed at {observation.LastPlacement}"));
        }

        var evidence = new List<string>();
        if (observation.LastWifiAt is { } wifi)
            evidence.Add($"Wi-Fi {wifi.ToLocalTime():HH:mm}");
        if (observation.LastPositionAt is { } position)
            evidence.Add($"GPS {position.ToLocalTime():HH:mm}");
        if (evidence.Count > 0)
            stack.Children.Add(LedgerChrome.Muted(string.Join(" · ", evidence)));
        if (!string.IsNullOrWhiteSpace(observation.LastError))
        {
            stack.Children.Add(new TextBlock
            {
                Text = observation.LastError,
                Foreground = GraphicalProfile.DangerBrush,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        stack.Children.Add(LedgerChrome.Secondary("Observation settings", openSettings));
        return LedgerChrome.Card(stack, GraphicalProfile.RaisedBrush);
    }

    static Control BuildDateBar(
        DateOnly date,
        Action previousDay,
        Action nextDay,
        Action today)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 8,
        };
        var previous = SmallButton("‹", previousDay);
        var next = SmallButton("›", nextDay);
        var todayButton = SmallButton(
            date == DateOnly.FromDateTime(DateTime.Now) ? "Today" : "Jump to today",
            today);
        row.Children.Add(previous);
        Grid.SetColumn(previous, 0);
        row.Children.Add(new TextBlock
        {
            Text = date.ToString("yyyy-MM-dd"),
            Foreground = GraphicalProfile.MutedBrush,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        Grid.SetColumn(row.Children[^1], 1);
        row.Children.Add(next);
        Grid.SetColumn(next, 3);
        row.Children.Add(todayButton);
        Grid.SetColumn(todayButton, 2);
        return row;
    }

    static Control BuildHero(
        PresenceInterval? current,
        PresenceDayProjection day,
        IReadOnlyList<TrackedLocation> locations)
    {
        var title = current is null ? "No confirmed location right now" : current.DisplayName;
        var detail = current is null
            ? locations.Count == 0
                ? "Start by adding Home or Work, then let the local observer collect evidence."
                : $"Watching {locations.Count} place{(locations.Count == 1 ? string.Empty : "s")}. A known Wi-Fi network counts as being there."
            : current.IsOpen
                ? $"Present since {FormatTime(current.StartedAt, day.DisplayTimeZone)}"
                : $"Present from {FormatTime(current.StartedAt, day.DisplayTimeZone)}";

        var stack = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                LedgerChrome.Chip(current is null ? "BETWEEN PLACES" : "PRESENT", accent: current is not null),
                new TextBlock
                {
                    Text = title,
                    FontSize = 24,
                    FontWeight = FontWeight.Bold,
                    Foreground = GraphicalProfile.TextBrush,
                    TextWrapping = TextWrapping.Wrap,
                },
                new TextBlock
                {
                    Text = detail,
                    Classes = { "body-copy" },
                    Foreground = GraphicalProfile.MutedBrush,
                },
            },
        };
        return LedgerChrome.Card(stack);
    }

    static Control BuildInterval(PresenceInterval interval, TimeZoneInfo timeZone)
    {
        var time = interval.EndedAt is { } end
            ? $"{FormatTime(interval.StartedAt, timeZone)} – {FormatTime(end, timeZone)}"
            : $"{FormatTime(interval.StartedAt, timeZone)} – now";
        var duration = interval.EndedAt is { } ended
            ? FormatDuration(ended - interval.StartedAt)
            : "Still present";

        var marker = new Border
        {
            Width = 8,
            MinHeight = 72,
            Background = interval.IsOpen
                ? GraphicalProfile.AccentFillBrush
                : GraphicalProfile.ActionBrush,
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 0, 12, 0),
        };
        var copy = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = interval.DisplayName,
                    FontSize = 17,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = GraphicalProfile.TextBrush,
                },
                new TextBlock
                {
                    Text = time,
                    Foreground = GraphicalProfile.MutedBrush,
                },
                new TextBlock
                {
                    Text = $"{duration} · {interval.Confidence}",
                    Foreground = GraphicalProfile.MutedBrush,
                },
            },
        };
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { marker, copy },
        };
        return LedgerChrome.Card(row);
    }

    static Control BuildEmptyState(PresenceDayProjection day, Action addLocation)
    {
        return LedgerChrome.Card(
            new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = day.Observations.Count == 0
                            ? "Nothing recorded for this day yet."
                            : "Evidence is still collecting.",
                        FontSize = 18,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = GraphicalProfile.TextBrush,
                        TextWrapping = TextWrapping.Wrap,
                    },
                    new TextBlock
                    {
                        Text = "Presence Ledger keeps the raw samples local and only turns sustained evidence into a segment.",
                        Classes = { "body-copy" },
                        Foreground = GraphicalProfile.MutedBrush,
                    },
                    LedgerChrome.Primary("Add your first location", addLocation),
                },
            },
            GraphicalProfile.RaisedBrush);
    }

    static Control BuildWatchingState(IReadOnlyList<TrackedLocation> locations)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock
        {
            Text = "Places being watched",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = GraphicalProfile.TextBrush,
        });
        foreach (var location in locations.OrderBy(item => item.DisplayName, StringComparer.Ordinal))
        {
            var network = location.Wifi is null
                ? "GPS when the network is unknown"
                : $"Wi-Fi {location.Wifi.Ssid} skips GPS";
            stack.Children.Add(new TextBlock
            {
                Text = $"{location.DisplayName} · {network}",
                Foreground = GraphicalProfile.MutedBrush,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        return LedgerChrome.Card(stack, GraphicalProfile.RaisedBrush);
    }

    static Control BuildEvidenceSummary(
        PresenceDayProjection day,
        IReadOnlyList<TrackedLocation> locations)
    {
        var lines = day.Observations.Count == 0
            ? "No samples for this day yet."
            : string.Join(
                Environment.NewLine,
                day.Observations.TakeLast(12).Select(item => FormatSample(item, locations)));
        return LedgerChrome.Card(
            new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    LedgerChrome.Eyebrow($"EVIDENCE · {day.Observations.Count}"),
                    new TextBlock
                    {
                        Text = lines,
                        Foreground = GraphicalProfile.TextBrush,
                        TextWrapping = TextWrapping.Wrap,
                        FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"),
                        FontSize = 12,
                    },
                },
            });
    }

    static string FormatSample(
        PresenceObservationDebugPoint item,
        IReadOnlyList<TrackedLocation> locations)
    {
        var time = item.At.ToLocalTime().ToString("HH:mm");
        var matched = WifiPlacement.Match(item.ConnectedSsid, locations);
        if (matched is not null)
        {
            return item.Position is { } placed
                ? $"{time} · {matched.DisplayName} via {item.ConnectedSsid} · {placed.Latitude:F5}, {placed.Longitude:F5}"
                : $"{time} · {matched.DisplayName} via {item.ConnectedSsid}";
        }

        if (item.Position is { } position)
        {
            var accuracy = item.AccuracyMeters is { } meters
                ? $" ±{meters:0} m"
                : string.Empty;
            var network = string.IsNullOrWhiteSpace(item.ConnectedSsid)
                ? "GPS"
                : $"GPS · {item.ConnectedSsid}";
            return $"{time} · {network} · {position.Latitude:F5}, {position.Longitude:F5}{accuracy}";
        }

        if (!string.IsNullOrWhiteSpace(item.ConnectedSsid))
            return $"{time} · Wi-Fi {item.ConnectedSsid} · not one of your places";

        return $"{time} · {item.WifiStatus} · GPS fix not available";
    }

    static Button SmallButton(string text, Action action)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 42,
            MinHeight = 42,
            Padding = new Thickness(10, 6),
            Background = GraphicalProfile.SurfaceBrush,
            Foreground = GraphicalProfile.TextBrush,
        };
        button.Click += (_, _) => action();
        return button;
    }

    static string FormatDate(DateOnly date) =>
        date == DateOnly.FromDateTime(DateTime.Now)
            ? "Today"
            : date.ToString("dddd, d MMMM");

    static string FormatTime(DateTimeOffset value, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTime(value, timeZone).ToString("HH:mm");

    static string FormatDuration(TimeSpan duration) =>
        duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}h {duration.Minutes:00}m"
            : $"{Math.Max(0, (int)Math.Round(duration.TotalMinutes))}m";
}
