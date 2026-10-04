namespace Novolis.Avalonia.Reach;

internal static class ReachClientEndpointStore
{
    internal static string ResolveDefault() =>
        OperatingSystem.IsAndroid()
            ? "10.0.2.2:19800"
            : "127.0.0.1:19800";

    internal static IReadOnlyList<string> Load()
    {
        try
        {
            var path = HistoryPath();
            return File.Exists(path)
                ? File.ReadAllLines(path)
                    .Where(static endpoint => !string.IsNullOrWhiteSpace(endpoint))
                    .Take(8)
                    .ToArray()
                : [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    internal static void Remember(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return;

        try
        {
            var endpoints = Load()
                .Where(item => !string.Equals(
                    item,
                    endpoint,
                    StringComparison.OrdinalIgnoreCase))
                .Prepend(endpoint)
                .Take(8)
                .ToArray();
            var path = HistoryPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllLines(path, endpoints);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string HistoryPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "Reach",
            "endpoints.txt");
}
