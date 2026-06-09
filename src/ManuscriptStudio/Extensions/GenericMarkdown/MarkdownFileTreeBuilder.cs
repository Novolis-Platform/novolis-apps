namespace ManuscriptStudio.Extensions.GenericMarkdown;

internal static class MarkdownFileTreeBuilder
{
    private static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".idea", ".vs", "bin", "obj", "node_modules", "artifacts",
    };

    public static IReadOnlyList<MarkdownTreeNode> Build(string root)
    {
        if (!Directory.Exists(root))
            return [];

        return BuildChildren(root);
    }

    private static List<MarkdownTreeNode> BuildChildren(string directory)
    {
        var nodes = new List<MarkdownTreeNode>();

        foreach (var subDir in Directory.EnumerateDirectories(directory).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(subDir);
            if (IgnoredDirectoryNames.Contains(name) || name.StartsWith('_'))
                continue;

            var children = BuildChildren(subDir);
            if (children.Count == 0)
                continue;

            nodes.Add(new MarkdownTreeNode(name, null, children));
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.md").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            nodes.Add(new MarkdownTreeNode(Path.GetFileName(file), file, []));

        return nodes;
    }
}
