using Novolis.Markup.Mermaid;

namespace ManuscriptStudio.Extensions.BookAuthoring.Views;

internal static class RelationshipMermaidBuilder
{
    public static string Build(IReadOnlyList<ChapterMetadataEntry> entries)
    {
        var chart = new Flowchart(Direction.TopToBottom);
        var characterNodes = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var chapterNode = new Node($"Ch {entry.SortKey:0.###}", Shape.Rounded);
            chart.AddNode(chapterNode);

            var characters = GetCharacters(entry);
            foreach (var name in characters)
            {
                if (!characterNodes.TryGetValue(name, out var charNode))
                {
                    charNode = new Node(name, Shape.Rectangle);
                    characterNodes[name] = charNode;
                    chart.AddNode(charNode);
                }

                chart.AddLink(new Link(charNode, chapterNode, entry.SortKey.ToString("0.###")));
            }

            var pov = GetTag(entry, "pov");
            if (!string.IsNullOrWhiteSpace(pov) && characterNodes.TryGetValue(pov, out var povNode))
                chart.AddLink(new Link(povNode, chapterNode, "POV"));
        }

        return chart.GetMermaidString();
    }

    private static List<string> GetCharacters(ChapterMetadataEntry entry)
    {
        var list = new List<string>();
        var chars = GetTag(entry, "characters");
        if (!string.IsNullOrWhiteSpace(chars))
        {
            foreach (var part in chars.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (part.Length > 0)
                    list.Add(part);
        }

        var pov = GetTag(entry, "pov");
        if (!string.IsNullOrWhiteSpace(pov) && !list.Contains(pov, StringComparer.OrdinalIgnoreCase))
            list.Add(pov);

        return list;
    }

    private static string? GetTag(ChapterMetadataEntry entry, string tag) =>
        entry.Tags.FirstOrDefault(t => t.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase)).Value;

}
