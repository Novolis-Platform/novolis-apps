using Novolis.Markup.Mermaid;

namespace ManuscriptStudio.Extensions.BookAuthoring.Views;

internal static class PlacesMermaidBuilder
{
    public static string Build(IReadOnlyList<ChapterMetadataEntry> entries)
    {
        var chart = new Flowchart(Direction.TopToBottom);
        var systems = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
        var locations = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries.OrderBy(e => e.SortKey))
        {
            var system = GetTag(entry, "system");
            var location = GetTag(entry, "location");
            var chapterNode = new Node($"Ch {entry.SortKey:0.###}", Shape.Rounded);
            chart.AddNode(chapterNode);

            if (!string.IsNullOrWhiteSpace(system))
            {
                if (!systems.TryGetValue(system, out var sysNode))
                {
                    sysNode = new Node(system, Shape.Rectangle);
                    systems[system] = sysNode;
                    chart.AddNode(sysNode);
                }

                if (!string.IsNullOrWhiteSpace(location))
                {
                    var locKey = $"{system}|{location}";
                    if (!locations.TryGetValue(locKey, out var locNode))
                    {
                        locNode = new Node(location, Shape.Rounded);
                        locations[locKey] = locNode;
                        chart.AddNode(locNode);
                        chart.AddLink(new Link(sysNode, locNode));
                    }

                    chart.AddLink(new Link(locNode, chapterNode));
                }
                else
                    chart.AddLink(new Link(sysNode, chapterNode));
            }
            else if (!string.IsNullOrWhiteSpace(location))
            {
                if (!locations.TryGetValue(location, out var locNode))
                {
                    locNode = new Node(location, Shape.Rounded);
                    locations[location] = locNode;
                    chart.AddNode(locNode);
                }

                chart.AddLink(new Link(locNode, chapterNode));
            }
        }

        return chart.GetMermaidString();
    }

    private static string? GetTag(ChapterMetadataEntry entry, string tag) =>
        entry.Tags.FirstOrDefault(t => t.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase)).Value;

}
