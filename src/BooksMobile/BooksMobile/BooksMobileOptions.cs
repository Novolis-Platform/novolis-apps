using BooksMobile.Services;
using Novolis.Avalonia.Mobile;

namespace BooksMobile;

/// <summary>Runtime configuration for BooksMobile.</summary>
public sealed class BooksMobileOptions
{
    public const string DefaultOwner = "frankhaugen";
    public const string BooksRepo = "books";
    public const string ReviewRepo = "galactic-confederation-review";
    public const string BooksContentPrefix = "src/";
    public const string ReviewContentPrefix = "docs/";

    /// <summary>Legacy alias for <see cref="BooksRepo"/>.</summary>
    public const string DefaultRepo = BooksRepo;

    /// <summary>Legacy alias for <see cref="BooksContentPrefix"/>.</summary>
    public const string DefaultContentPrefix = BooksContentPrefix;

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
        LocalWorkspace = Environment.GetEnvironmentVariable("BOOKSMOBILE_LOCAL_WORKSPACE")?.Trim();

        var envRepo = Environment.GetEnvironmentVariable("BOOKSMOBILE_REPO_NAME")?.Trim();
        var envPrefix = Environment.GetEnvironmentVariable("BOOKSMOBILE_CONTENT_PREFIX")?.Trim();
        if (!string.IsNullOrWhiteSpace(envRepo) || !string.IsNullOrWhiteSpace(envPrefix))
        {
            RepoName = string.IsNullOrWhiteSpace(envRepo) ? BooksRepo : envRepo;
            ContentPrefix = NormalizePrefix(string.IsNullOrWhiteSpace(envPrefix) ? BooksContentPrefix : envPrefix);
            Mode = string.Equals(RepoName, ReviewRepo, StringComparison.OrdinalIgnoreCase)
                ? WorkspaceMode.Review
                : WorkspaceMode.Books;
        }
        else
        {
            Mode = WorkspaceMode.Books;
            ApplyMode(WorkspaceMode.Books);
        }
    }

    /// <summary>GitHub OAuth App client id (public). Re-reads env/files each get.</summary>
    public string GitHubClientId
    {
        get => ResolveClientId(_appDataPaths);
        set => _overrideClientId = value;
    }

    public string RepoOwner { get; set; }

    public string RepoName { get; set; } = BooksRepo;

    /// <summary>Sparse mirror path prefix (<c>src/</c> for NMP/1, or <c>docs/</c> for Review).</summary>
    public string ContentPrefix { get; set; } = BooksContentPrefix;

    /// <summary>Active workspace mode (Books NMP vs Review MkDocs).</summary>
    public WorkspaceMode Mode { get; private set; }

    /// <summary>
    /// Optional absolute path to a local git checkout (desktop testing).
    /// When set, the app opens that folder without GitHub auth.
    /// </summary>
    public string? LocalWorkspace { get; set; }

    /// <summary>Binds app-data paths so client id and mode can be read from disk.</summary>
    public void BindAppDataPaths(IAppDataPaths paths)
    {
        _appDataPaths = paths ?? throw new ArgumentNullException(nameof(paths));
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BOOKSMOBILE_REPO_NAME")))
            return;

        var modePath = ModeFilePath(paths);
        if (File.Exists(modePath)
            && Enum.TryParse<WorkspaceMode>(File.ReadAllText(modePath).Trim(), ignoreCase: true, out var mode))
        {
            ApplyMode(mode);
        }
    }

    /// <summary>Switches Books vs Review remotes and persists the choice.</summary>
    public void ApplyMode(WorkspaceMode mode)
    {
        Mode = mode;
        if (mode == WorkspaceMode.Review)
        {
            RepoName = ReviewRepo;
            ContentPrefix = ReviewContentPrefix;
        }
        else
        {
            RepoName = BooksRepo;
            ContentPrefix = BooksContentPrefix;
        }

        ContentPrefix = NormalizePrefix(ContentPrefix);
        if (_appDataPaths is not null)
        {
            try
            {
                Directory.CreateDirectory(_appDataPaths.RootDirectory);
                File.WriteAllText(ModeFilePath(_appDataPaths), mode.ToString());
            }
            catch
            {
                // Persistence is best-effort.
            }
        }
    }

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

    static string ModeFilePath(IAppDataPaths paths) =>
        Path.Combine(paths.RootDirectory, "workspace-mode.txt");

    static string NormalizePrefix(string prefix)
    {
        var p = prefix.Replace('\\', '/').Trim();
        if (!p.EndsWith('/'))
            p += "/";
        return p;
    }

    static string EnvOr(string name, string fallback)
    {
        var v = Environment.GetEnvironmentVariable(name)?.Trim();
        return string.IsNullOrWhiteSpace(v) ? fallback : v;
    }
}
