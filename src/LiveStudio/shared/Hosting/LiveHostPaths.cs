namespace LiveStudio.Shared.Hosting;

public static class LiveHostPaths
{
    public static string ResolveHostProjectPath()
    {
        var appsRoot = FindAncestorDirectory("novolis-apps");
        var projectPath = Path.Combine(appsRoot, "src", "LiveStudio", "host", "LiveStudio.Host.csproj");

        if (!File.Exists(projectPath))
            throw new FileNotFoundException("Unable to locate the Novolis Audio live host project.", projectPath);

        return projectPath;
    }

    public static string ResolveStudioProjectPath()
    {
        var appsRoot = FindAncestorDirectory("novolis-apps");
        var projectPath = Path.Combine(appsRoot, "src", "LiveStudio", "studio", "LiveStudio.csproj");

        if (!File.Exists(projectPath))
            throw new FileNotFoundException("Unable to locate the Novolis Audio live studio project.", projectPath);

        return projectPath;
    }

    public static bool TryResolvePublishedHostExecutable(string publishRoot, out string executablePath, out bool useDotNet)
    {
        var overridePath = Environment.GetEnvironmentVariable("NOVOLIS_AUDIO_LIVE_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            var candidate = Path.GetFullPath(overridePath);
            if (File.Exists(candidate))
            {
                executablePath = candidate;
                useDotNet = Path.GetExtension(candidate).Equals(".dll", StringComparison.OrdinalIgnoreCase);
                return true;
            }
        }

        var hostName = OperatingSystem.IsWindows() ? "Novolis.Audio.Live.Host.exe" : "Novolis.Audio.Live.Host";
        foreach (var hostDir in GetHostSearchRoots(publishRoot))
        {
            var candidateHost = Path.Combine(hostDir, hostName);
            if (File.Exists(candidateHost))
            {
                executablePath = candidateHost;
                useDotNet = false;
                return true;
            }

            var candidateDll = Path.Combine(hostDir, "Novolis.Audio.Live.Host.dll");
            if (File.Exists(candidateDll))
            {
                executablePath = candidateDll;
                useDotNet = true;
                return true;
            }
        }

        executablePath = string.Empty;
        useDotNet = false;
        return false;
    }

    public static bool TryResolvePublishedStudioExecutable(string publishRoot, out string executablePath, out bool useDotNet)
    {
        var studioName = OperatingSystem.IsWindows() ? "Novolis.Audio.Live.Studio.exe" : "Novolis.Audio.Live.Studio";
        foreach (var studioDir in GetStudioSearchRoots(publishRoot))
        {
            var candidateStudio = Path.Combine(studioDir, studioName);
            if (File.Exists(candidateStudio))
            {
                executablePath = candidateStudio;
                useDotNet = false;
                return true;
            }

            var candidateDll = Path.Combine(studioDir, "Novolis.Audio.Live.Studio.dll");
            if (File.Exists(candidateDll))
            {
                executablePath = candidateDll;
                useDotNet = true;
                return true;
            }
        }

        executablePath = string.Empty;
        useDotNet = false;
        return false;
    }

    private static IEnumerable<string> GetHostSearchRoots(string publishRoot)
    {
        yield return Path.Combine(publishRoot, "host");
        yield return Path.GetFullPath(Path.Combine(publishRoot, "..", "host"));
    }

    private static IEnumerable<string> GetStudioSearchRoots(string publishRoot)
    {
        yield return publishRoot;
        yield return Path.GetFullPath(Path.Combine(publishRoot, ".."));
    }

    public static string FindAncestorDirectory(string name)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (string.Equals(current.Name, name, StringComparison.OrdinalIgnoreCase))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException($"Unable to locate the '{name}' repository root from {AppContext.BaseDirectory}.");
    }
}
