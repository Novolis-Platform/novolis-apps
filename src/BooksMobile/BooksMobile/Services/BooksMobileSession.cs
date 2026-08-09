using Novolis.Avalonia.Mobile;
using Novolis.IO.GitHub;
using Novolis.Manuscript;

namespace BooksMobile.Services;

/// <summary>Session: auth, mirror, catalog, open chapter / selection.</summary>
public sealed class BooksMobileSession
{
    readonly ISecureTokenStore _tokens;
    readonly IAppDataPaths _paths;
    readonly IDeviceFlowPresenter _presenter;
    readonly BooksMobileOptions _options;
    readonly GitHubDeviceAuth _deviceAuth = new();

    SparseRepoMirror? _mirror;
    ManuscriptWorkspace? _workspace;
    string? _localWorkspace;
    string? _openRelativePath;
    string? _openFilePath;

    public BooksMobileSession(
        ISecureTokenStore tokens,
        IAppDataPaths paths,
        IBrowserLauncher browser,
        IDeviceFlowPresenter presenter,
        BooksMobileOptions options)
    {
        _tokens = tokens;
        _paths = paths;
        _ = browser;
        _presenter = presenter;
        _options = options;
        _options.BindAppDataPaths(paths);
        _localWorkspace = NormalizeLocal(_options.LocalWorkspace);
    }

    public string WorkspaceRoot => _localWorkspace ?? _paths.WorkspaceDirectory;

    public bool IsLocalWorkspace => !string.IsNullOrWhiteSpace(_localWorkspace);

    public WorkspaceMode Mode => _options.Mode;

    public bool IsReviewMode => _options.Mode == WorkspaceMode.Review;

    public string? Status { get; private set; }

    public string? UserCode { get; private set; }

    public bool IsSignedIn { get; private set; }

    public SparseRepoMirror? Mirror => _mirror;

    public ManuscriptWorkspace? Workspace => _workspace;

    public string? OpenRelativePath => _openRelativePath;

    public string? OpenFilePath => _openFilePath;

    public int DirtyCount => _mirror?.DirtyCount ?? 0;

    public string? Branch => _mirror?.Branch;

    /// <summary>Raised when Status, UserCode, or sign-in state changes (may be off the UI thread).</summary>
    public event EventHandler? Changed;

    public async Task<bool> TryRestoreSessionAsync(CancellationToken cancellationToken = default)
    {
        if (IsLocalWorkspace)
        {
            IsSignedIn = true;
            Status = $"Local workspace: {WorkspaceRoot}";
            TryOpenWorkspace();
            NotifyChanged();
            return true;
        }

        var token = await _tokens.GetAsync(SecureTokenKeys.GitHubOAuthAccessToken, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            IsSignedIn = false;
            NotifyChanged();
            return false;
        }

        if (!await GitHubAccessToken.TryValidateAsync(token, "Novolis.BooksMobile", cancellationToken)
                .ConfigureAwait(false))
        {
            await ClearStoredAuthAsync(cancellationToken).ConfigureAwait(false);
            IsSignedIn = false;
            Status = "GitHub session expired — sign in again.";
            NotifyChanged();
            return false;
        }

        BindMirror(token);
        IsSignedIn = true;
        Status = $"Signed in · {_options.RepoName}";
        TryOpenWorkspace();
        NotifyChanged();
        return true;
    }

    public async Task SignInAsync(CancellationToken cancellationToken = default)
    {
        if (IsLocalWorkspace)
        {
            IsSignedIn = true;
            Status = $"Local workspace: {WorkspaceRoot}";
            TryOpenWorkspace();
            NotifyChanged();
            return;
        }

        var clientId = _options.GitHubClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            Status =
                "One-time setup: create a GitHub OAuth App with Device Flow enabled, then set the public Client ID (not a secret). See README.";
            NotifyChanged();
            return;
        }

        Status = "Opening GitHub… sign in with passkey / GitHub app, then approve.";
        NotifyChanged();
        var scope = clientId.StartsWith("Iv", StringComparison.OrdinalIgnoreCase) ? null : "repo";
        var device = await _deviceAuth.RequestDeviceCodeAsync(clientId, scope, cancellationToken)
            .ConfigureAwait(false);
        UserCode = device.UserCode;
        Status = $"Enter this code on GitHub (if not pre-filled): {device.UserCode}";
        NotifyChanged();
        await _presenter.PresentAsync(device.UserCode, device.VerificationUriComplete, cancellationToken)
            .ConfigureAwait(false);
        Status = $"Waiting for GitHub approval… code {device.UserCode}";
        NotifyChanged();

        var token = await _deviceAuth.WaitForAccessTokenAsync(clientId, device, cancellationToken)
            .ConfigureAwait(false);
        await _tokens.SetAsync(SecureTokenKeys.GitHubOAuthAccessToken, token, cancellationToken)
            .ConfigureAwait(false);
        BindMirror(token);
        IsSignedIn = true;
        UserCode = null;
        Status = $"Signed in · {_options.RepoName}";
        TryOpenWorkspace();
        NotifyChanged();
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        if (IsLocalWorkspace)
        {
            Status = "Local workspace stays open (no GitHub session).";
            NotifyChanged();
            return;
        }

        await ClearStoredAuthAsync(cancellationToken).ConfigureAwait(false);
        _mirror = null;
        _workspace = null;
        _openFilePath = null;
        _openRelativePath = null;
        IsSignedIn = false;
        Status = "Signed out.";
        NotifyChanged();
    }

    /// <summary>Switches Books (NMP) vs Review (MkDocs) remotes and pulls.</summary>
    public async Task SwitchModeAsync(WorkspaceMode mode, CancellationToken cancellationToken = default)
    {
        if (_options.Mode == mode && _mirror is not null)
        {
            Status = $"Already on {_options.RepoName}.";
            NotifyChanged();
            return;
        }

        _options.ApplyMode(mode);
        _openFilePath = null;
        _openRelativePath = null;
        _workspace = null;

        if (IsLocalWorkspace)
        {
            Status = $"Local mode — switch remotes only applies to GitHub. Still at {WorkspaceRoot}.";
            TryOpenWorkspace();
            NotifyChanged();
            return;
        }

        if (!IsSignedIn)
        {
            Status = mode == WorkspaceMode.Review
                ? "Review mode selected — sign in, then Pull."
                : "Books mode selected — sign in, then Pull.";
            NotifyChanged();
            return;
        }

        var token = await _tokens.GetAsync(SecureTokenKeys.GitHubOAuthAccessToken, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            IsSignedIn = false;
            Status = "GitHub session missing — sign in again.";
            NotifyChanged();
            return;
        }

        BindMirror(token);
        Status = $"Switched to {_options.RepoName} — pulling…";
        NotifyChanged();
        await PullAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task PullAsync(CancellationToken cancellationToken = default)
    {
        if (IsLocalWorkspace)
        {
            Status = "Local mode — files are already on disk. Edit and save.";
            TryOpenWorkspace();
            NotifyChanged();
            await Task.CompletedTask.ConfigureAwait(false);
            return;
        }

        EnsureMirror();
        Status = $"Pulling {_options.RepoName}…";
        NotifyChanged();
        var result = await _mirror!.PullAsync(cancellationToken).ConfigureAwait(false);
        if (result.RequiresReauthentication)
        {
            await RequireReauthenticationAsync(result.Message, cancellationToken).ConfigureAwait(false);
            return;
        }

        Status = result.Message;
        if (result.Ok)
            TryOpenWorkspace();
        NotifyChanged();
    }

    public async Task SaveCommitPushAsync(
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        if (IsLocalWorkspace)
        {
            Status = "Saved on disk (local workspace — use git in the repo folder to commit/push).";
            NotifyChanged();
            await Task.CompletedTask.ConfigureAwait(false);
            return;
        }

        EnsureMirror();
        if (!string.IsNullOrWhiteSpace(_openFilePath) && !string.IsNullOrWhiteSpace(_openRelativePath))
            _mirror!.NoteDirty(_openRelativePath);

        Status = "Save/Commit/Push…";
        NotifyChanged();
        var result = await _mirror!.SaveCommitPushAsync(message, cancellationToken)
            .ConfigureAwait(false);
        if (result.RequiresReauthentication)
        {
            await RequireReauthenticationAsync(result.Message, cancellationToken).ConfigureAwait(false);
            return;
        }

        Status = result.Message;
        NotifyChanged();
    }

    public void NoteDirtyPaths(IEnumerable<string> relativePaths)
    {
        if (_mirror is null)
            return;
        foreach (var rel in relativePaths)
        {
            if (!string.IsNullOrWhiteSpace(rel))
                _mirror.NoteDirty(rel.Replace('\\', '/'));
        }
    }

    async Task RequireReauthenticationAsync(string? message, CancellationToken cancellationToken)
    {
        await ClearStoredAuthAsync(cancellationToken).ConfigureAwait(false);
        _mirror = null;
        _workspace = null;
        _openFilePath = null;
        _openRelativePath = null;
        IsSignedIn = false;
        UserCode = null;
        Status = string.IsNullOrWhiteSpace(message)
            ? "GitHub session expired — sign in again."
            : message;
        NotifyChanged();
    }

    async Task ClearStoredAuthAsync(CancellationToken cancellationToken) =>
        await _tokens.RemoveAsync(SecureTokenKeys.GitHubOAuthAccessToken, cancellationToken)
            .ConfigureAwait(false);

    void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public void OpenChapter(string absolutePath)
    {
        _openFilePath = absolutePath;
        _openRelativePath = Path.GetRelativePath(WorkspaceRoot, absolutePath).Replace('\\', '/');
    }

    public void NoteCurrentDirty()
    {
        if (!string.IsNullOrWhiteSpace(_openRelativePath))
            _mirror?.NoteDirty(_openRelativePath);
    }

    public IReadOnlyList<SeriesInfo> LoadSeries() =>
        WorkspaceCatalog.LoadSeries(WorkspaceRoot, reviewMode: IsReviewMode);

    public IReadOnlyList<BookInfo> LoadStandaloneBooks() =>
        WorkspaceCatalog.LoadBooks(WorkspaceRoot, reviewMode: IsReviewMode);

    void BindMirror(string token)
    {
        var client = SparseRepoMirror.CreateClient(token, "Novolis.BooksMobile");
        _mirror = new SparseRepoMirror(client, new SparseRepoMirrorOptions
        {
            Owner = _options.RepoOwner,
            Name = _options.RepoName,
            WorkspaceRoot = WorkspaceRoot,
            ContentPrefix = _options.ContentPrefix,
        });
    }

    void EnsureMirror()
    {
        if (_mirror is null)
            throw new InvalidOperationException("Sign in first.");
    }

    void TryOpenWorkspace()
    {
        if (ManuscriptWorkspace.TryOpen(WorkspaceRoot, out var ws) && ws is not null)
            _workspace = ws;
        else
            _workspace = null;
    }

    static string? NormalizeLocal(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        var full = Path.GetFullPath(path.Trim());
        return Directory.Exists(full) ? full : null;
    }
}
