using Spectre.Console;

namespace Novolis.Hours.Client.Cli;

/// <summary>Type-ahead / numbered catalog for the employee CLI.</summary>
internal static class HoursPalette
{
    /// <summary>Canonical palette verbs.</summary>
    public static readonly string[] Catalog =
    [
        "week",
        "day",
        "record",
        "paint",
        "review",
        "pressure",
        "settings",
        "quit",
    ];

    /// <summary>Prompts until the person picks a known verb or quits.</summary>
    public static string Prompt(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);
        var numbered = string.Join("  ", Catalog.Select((name, index) => $"{index + 1}. {name}"));
        console.MarkupLine($"[grey]{numbered}[/]");
        var raw = console.Prompt(new TextPrompt<string>("Hours").AllowEmpty());
        return Resolve(raw);
    }

    /// <summary>Resolves a typed or numbered catalog entry.</summary>
    public static string Resolve(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "week";
        }

        var trimmed = raw.Trim();
        if (int.TryParse(trimmed, out var number) && number >= 1 && number <= Catalog.Length)
        {
            return Catalog[number - 1];
        }

        var exact = Catalog.FirstOrDefault(name =>
            string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        return HoursLevenshtein.Suggest(trimmed.ToLowerInvariant(), Catalog) ?? trimmed.ToLowerInvariant();
    }
}
