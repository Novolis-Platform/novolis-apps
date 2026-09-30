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
        DateTimeOffset now,
        Action previousDay,
        Action nextDay,
        Action today,
        Action openMap,
        Action addLocation,
        string observationStatus)
    {
        var content = new StackPanel
        {
            Margin = new Thickness(16, 12, 16, 28),
            Spacing = 14,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var eyebrow = new TextBlock
        {
            Text = "YOUR DAY",
            Classes = { "eyebrow" },
            Foreground = GraphicalProfile.AccentBrush,
        };
        content.Children.Add(eyebrow);
        content.Children.Add(new TextBlock
        {
            Text = FormatDate(day.DisplayDate),
            Classes = { "page-title" },
            Foreground = GraphicalProfile.TextBrush,
        });
        content.Children.Add(new TextBlock
        {
            Text = "A private, explainable record of where your configured locations saw you.",
            Classes = { "body-copy" },
            Foreground = GraphicalProfile.MutedBrush,
        });
        content.Children.Add(Card(
            new TextBlock
            {
                Text = observationStatus,
                Foreground = GraphicalProfile.MutedBrush,
                TextWrapping = TextWrapping.Wrap,
            },
            GraphicalProfile.SurfaceBrush));

        content.Children.Add(BuildDateBar(
            day.DisplayDate,
            previousDay,
            nextDay,
            today));

        var current = day.Intervals
            .Where(interval =>
                interval.StartedAt <= now
                && (interval.EndedAt is null || now < interval.EndedAt))
            .OrderByDescending(interval => interval.StartedAt)
            .FirstOrDefault();
        content.Children.Add(BuildHero(current, day));

        var mapButton = new Button
        {
            Content = "Open day map",
            Classes = { "primary-button" },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = GraphicalProfile.AccentFillBrush,
            Foreground = GraphicalProfile.TextBrush,
        };
        mapButton.Click += (_, _) => openMap();
        content.Children.Add(mapButton);

        var timelineHeader = new DockPanel();
        var timelineTitle = new TextBlock
        {
            Text = "TIMELINE",
            Classes = { "eyebrow" },
            Foreground = GraphicalProfile.AccentBrush,
        };
        DockPanel.SetDock(timelineTitle, Dock.Left);
        timelineHeader.Children.Add(timelineTitle);
        var count = new TextBlock
        {
            Text = $"{day.Intervals.Count} segment{(day.Intervals.Count == 1 ? string.Empty : "s")}",
            Foreground = GraphicalProfile.MutedBrush,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        DockPanel.SetDock(count, Dock.Right);
        timelineHeader.Children.Add(count);
        content.Children.Add(timelineHeader);

        if (day.Intervals.Count == 0)
        {
            content.Children.Add(BuildEmptyState(day, addLocation));
        }
        else
        {
            foreach (var interval in day.Intervals)
                content.Children.Add(BuildInterval(interval, day.DisplayTimeZone));
        }

        content.Children.Add(BuildEvidenceSummary(day));

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

    static Control BuildHero(PresenceInterval? current, PresenceDayProjection day)
    {
        var title = current is null ? "No confirmed location right now" : current.DisplayName;
        var detail = current is null
            ? day.Intervals.Count == 0
                ? "Start by adding Home or Work, then let the local observer collect evidence."
                : "The ledger is between configured locations."
            : current.IsOpen
                ? $"Present since {FormatTime(current.StartedAt, day.DisplayTimeZone)}"
                : $"Present from {FormatTime(current.StartedAt, day.DisplayTimeZone)}";

        var badge = new Border
        {
            Padding = new Thickness(10, 6),
            Background = current is null
                ? GraphicalProfile.ActionSoftBrush
                : GraphicalProfile.AccentFillBrush,
            CornerRadius = new CornerRadius(14),
            Child = new TextBlock
            {
                Text = current is null ? "BETWEEN PLACES" : "PRESENT",
                Classes = { "eyebrow" },
                Foreground = GraphicalProfile.TextBrush,
            },
        };
        var stack = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                badge,
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
        return Card(stack, GraphicalProfile.SurfaceBrush);
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
            Background = GraphicalProfile.ActionBrush,
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
        return Card(row, GraphicalProfile.SurfaceBrush);
    }

    static Control BuildEmptyState(PresenceDayProjection day, Action addLocation)
    {
        var add = new Button
        {
            Content = "Add your first location",
            Classes = { "primary-button" },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = GraphicalProfile.ActionBrush,
            Foreground = Brushes.White,
        };
        add.Click += (_, _) => addLocation();
        return Card(
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
                    add,
                },
            },
            GraphicalProfile.RaisedBrush);
    }

    static Control BuildEvidenceSummary(PresenceDayProjection day)
    {
        var expander = new Expander
        {
            Header = $"Evidence details · {day.Observations.Count} local sample{(day.Observations.Count == 1 ? string.Empty : "s")}",
            Foreground = GraphicalProfile.MutedBrush,
            Content = new TextBlock
            {
                Text = day.Observations.Count == 0
                    ? "No raw samples are available for this display day."
                    : string.Join(
                        Environment.NewLine,
                        day.Observations.Take(30).Select(item =>
                            $"{item.At.ToLocalTime():g} · {item.WifiStatus} · "
                            + (item.Position is { } position
                                ? $"{position.Latitude:F5}, {position.Longitude:F5}"
                                : "no position"))),
                Foreground = GraphicalProfile.MutedBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0),
            },
        };
        return expander;
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

    static string FormatDate(DateOnly date) =>
        date == DateOnly.FromDateTime(DateTime.Now)
            ? "Today"
            : date.ToString("dddd, d MMMM");

    static string FormatTime(DateTimeOffset value, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTime(value, timeZone).ToString("HH:mm");

    static string FormatDuration(TimeSpan duration) =>
        duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}h {duration.Minutes:00}m"
            : $"{Math.Max(1, (int)duration.TotalMinutes)} min";
}
