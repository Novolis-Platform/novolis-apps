namespace Novolis.Hours.Client.Cli;

/// <summary>Tiny edit-distance helper for palette suggestions.</summary>
internal static class HoursLevenshtein
{
    /// <summary>Returns the closest catalog name, or null when nothing is close.</summary>
    public static string? Suggest(string input, IReadOnlyList<string> catalog)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        ArgumentNullException.ThrowIfNull(catalog);
        string? best = null;
        var bestDistance = int.MaxValue;
        foreach (var name in catalog)
        {
            var distance = Distance(input, name);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = name;
            }
        }

        return bestDistance <= Math.Max(2, input.Length / 2) ? best : null;
    }

    private static int Distance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];
        for (var j = 0; j <= right.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
