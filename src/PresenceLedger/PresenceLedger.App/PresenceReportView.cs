using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;
using PresenceLedger.Core;

namespace PresenceLedger.App;

/// <summary>Multi-day arrivals and departures for each place.</summary>
internal static class PresenceReportView
{
    static readonly IBrush[] PlaceBrushes =
    [
        GraphicalProfile.AccentBrush,
        GraphicalProfile.ActionBrush,
        GraphicalProfile.AccentFillBrush,
        GraphicalProfile.DangerBrush,
    ];

    public static Control Build(
        AttendanceReport report,
        TimeZoneInfo zone,
        DateTimeOffset asOf,
        Func<Task> export)
    {
        var content = new StackPanel
        {
            Margin = new Thickness(16, 12, 16, 28),
            Spacing = 14,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        content.Children.Add(new TextBlock
        {
            Text = "OVER TIME",
            Classes = { "eyebrow" },
            Foreground = GraphicalProfile.AccentBrush,
        });
        content.Children.Add(new TextBlock
        {
            Text = "Where you have been",
            FontSize = 26,
            FontWeight = FontWeight.SemiBold,
            Foreground = GraphicalProfile.TextBrush,
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(new TextBlock
        {
            Text = "Arrivals and departures for each place, across every retained day.",
            Foreground = GraphicalProfile.MutedBrush,
            TextWrapping = TextWrapping.Wrap,
        });

        if (report.Places.Count == 0)
        {
            content.Children.Add(Card(new TextBlock
            {
                Text = "No arrivals or departures yet. A place appears here after a stay is confirmed.",
                Foreground = GraphicalProfile.TextBrush,
                TextWrapping = TextWrapping.Wrap,
            }));
            return content;
        }

        var exportButton = new Button
        {
            Content = "Export report",
            Classes = { "primary-button" },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 48,
            Background = GraphicalProfile.AccentFillBrush,
            Foreground = GraphicalProfile.TextBrush,
        };
        exportButton.Click += async (_, _) => await export();
        content.Children.Add(exportButton);

        var culture = CultureInfo.CurrentCulture;
        for (var index = 0; index < report.Places.Count; index++)
        {
            var place = report.Places[index];
            var body = new StackPanel { Spacing = 8 };
            body.Children.Add(new TextBlock
            {
                Text = place.DisplayName,
                FontSize = 20,
                FontWeight = FontWeight.SemiBold,
                Foreground = PlaceBrushes[index % PlaceBrushes.Length],
            });
            body.Children.Add(new TextBlock
            {
                Text = AttendanceReport.Summary(place, zone, asOf),
                Foreground = GraphicalProfile.MutedBrush,
            });
            DateOnly? day = null;
            foreach (var mark in report.Marks(zone, asOf).Where(mark => mark.LocationId == place.LocationId))
            {
                if (day != mark.Day)
                {
                    day = mark.Day;
                    body.Children.Add(new TextBlock
                    {
                        Text = mark.Day.ToString("ddd d MMM yyyy", culture),
                        Margin = new Thickness(0, 6, 0, 0),
                        Foreground = GraphicalProfile.TextBrush,
                    });
                }

                body.Children.Add(new TextBlock
                {
                    Text = mark.Line,
                    Margin = new Thickness(12, 0, 0, 0),
                    Foreground = GraphicalProfile.TextBrush,
                });
            }

            content.Children.Add(Card(body));
        }

        content.Children.Add(new TextBlock
        {
            Text = "EACH DAY",
            Classes = { "eyebrow" },
            Foreground = GraphicalProfile.AccentBrush,
            Margin = new Thickness(0, 8, 0, 0),
        });
        DateOnly? sharedDay = null;
        var dayCard = new StackPanel { Spacing = 6 };
        foreach (var mark in report.Marks(zone, asOf))
        {
            if (sharedDay != mark.Day)
            {
                if (sharedDay is not null)
                    content.Children.Add(Card(dayCard));

                sharedDay = mark.Day;
                dayCard = new StackPanel { Spacing = 6 };
                dayCard.Children.Add(new TextBlock
                {
                    Text = mark.Day.ToString("ddd d MMM yyyy", culture),
                    FontWeight = FontWeight.SemiBold,
                    Foreground = GraphicalProfile.TextBrush,
                });
            }

            dayCard.Children.Add(new TextBlock
            {
                Text = $"{mark.Place} · {mark.Line}",
                Foreground = GraphicalProfile.TextBrush,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        if (sharedDay is not null)
            content.Children.Add(Card(dayCard));

        return content;
    }

    static Border Card(Control child) =>
        new()
        {
            Child = child,
            Padding = new Thickness(16),
            Background = GraphicalProfile.SurfaceBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
        };
}
