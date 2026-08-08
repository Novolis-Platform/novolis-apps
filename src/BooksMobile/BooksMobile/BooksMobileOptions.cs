using Novolis.Avalonia.Mobile;

namespace BooksMobile;

/// <summary>Runtime configuration for BooksMobile.</summary>
public sealed class BooksMobileOptions
{
    public const string DefaultOwner = "frankhaugen";
    public const string DefaultRepo = "books";
    public const string DefaultContentPrefix = "src/";

    /// <summary>
    /// Public OAuth App client id (Device Flow). Not a secret — safe to ship in the app.
    /// Create once at https://github.com/settings/applications/new (Enable Device Flow).
    /// </summary>
    public const string DefaultGitHubClientId = "Iv23lieombuYLqbFAsY9";

    IAppDataPaths? _appDataPaths;
    string? _overrideClientId;

    public BooksMobileOptions()
    {
        RepoOwner = EnvOr("BOOKSMOBILE_REPO_OWNER", DefaultOwner);
        RepoName = EnvOr("BOOKSMOBILE_REPO_NAME", DefaultRepo);
        ContentPrefix = EnvOr("BOOKSMOBILE_CONTENT_PREFIX", DefaultContentPrefix);
        if (!ContentPrefix.EndsWith('/'))
            ContentPrefix += "/";
        LocalWorkspace = Environment.GetEnvironmentVariable("BOOKSMOBILE_LOCAL_WORKSPACE")?.Trim();
    }

    /// <summary>GitHub OAuth App client id (public). Re-reads env/files each get.</summary>
    public string GitHubClientId
    {
        get => ResolveClientId(_appDataPaths);
        set => _overrideClientId = value;
    }

    public string RepoOwner { get; set; }

    public string RepoName { get; set; }

    /// <summary>Sparse mirror path prefix (<c>src/</c> for NMP/1, or a custom prefix).</summary>
    public string ContentPrefix { get; set; }

    /// <summary>
    /// Optional absolute path to a local git checkout (desktop testing).
    /// When set, the app opens that folder without GitHub auth.
    /// </summary>
    public string? LocalWorkspace { get; set; }

    /// <summary>Binds app-data paths so client id can be read from <c>{Root}/github-client-id.txt</c>.</summary>
    public void BindAppDataPaths(IAppDataPaths paths) =>
        _appDataPaths = paths ?? throw new ArgumentNullException(nameof(paths));

    string ResolveClientId(IAppDataPaths? paths)
    {
        if (!string.IsNullOrWhiteSpace(_overrideClientId))
            return _overrideClientId.Trim();

        var fromEnv = Environment.GetEnvironmentVariable("BOOKSMOBILE_GITHUB_CLIENT_ID")?.Trim();
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv;

        foreach (var path in CandidateClientIdFiles(paths))
        {
            if (!File.Exists(path))
                continue;
            var text = File.ReadAllText(path).Trim();
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        return DefaultGitHubClientId;
    }

    static IEnumerable<string> CandidateClientIdFiles(IAppDataPaths? paths)
    {
        if (paths is not null)
            yield return Path.Combine(paths.RootDirectory, "github-client-id.txt");

        yield return Path.Combine(AppContext.BaseDirectory, "github-client-id.txt");
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(local))
            yield return Path.Combine(local, "Novolis", "BooksMobile", "github-client-id.txt");
    }

    static string EnvOr(string name, string fallback)
    {
        var v = Environment.GetEnvironmentVariable(name)?.Trim();
        return string.IsNullOrWhiteSpace(v) ? fallback : v;
    }
}
