using System.Text;

namespace LiveStudio;

/// <summary>Normalizes the tiny live REPL surface before sending to <c>LiveReplSyntaxCompiler</c>.</summary>
internal static class LiveReplSource
{
    public static string Normalize(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var builder = new StringBuilder(source.Length);
        foreach (var rawLine in source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = rawLine;
            var comment = line.IndexOf("//", StringComparison.Ordinal);
            if (comment >= 0)
                line = line[..comment];

            line = line.Trim();
            if (line.Length == 0)
                continue;

            if (builder.Length > 0)
                builder.Append(' ');
            builder.Append(line);
        }

        return builder.ToString().Trim();
    }
}
