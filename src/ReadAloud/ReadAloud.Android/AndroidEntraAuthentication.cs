using Android.OS;
using Azure.Core;
using Microsoft.Identity.Client;

namespace ReadAloud.Android;

/// <summary>
/// Signs in the personal Microsoft account that is a guest in the app's
/// directory. The device broker is left unused so a work account signed in
/// on the phone is not offered. The prompt stays in MSAL's embedded WebView.
/// </summary>
public sealed class AndroidEntraAuthentication
{
    public const string ManagementScope = "https://management.azure.com/.default";
    public const string SpeechScope = "https://cognitiveservices.azure.com/.default";

    readonly SemaphoreSlim _gate = new(1, 1);
    readonly string _loginHint;
    IPublicClientApplication? _application;
    string? _homeAccountId;

    public AndroidEntraAuthentication(
        string clientId,
        string tenantId,
        string loginHint)
    {
        ClientId = string.IsNullOrWhiteSpace(clientId)
            ? throw new ArgumentException("Client id is required.", nameof(clientId))
            : clientId;
        TenantId = string.IsNullOrWhiteSpace(tenantId)
            ? throw new ArgumentException("Tenant id is required.", nameof(tenantId))
            : tenantId;
        _loginHint = string.IsNullOrWhiteSpace(loginHint)
            ? throw new ArgumentException("Login hint is required.", nameof(loginHint))
            : loginHint.Trim();
    }

    public string ClientId { get; }

    public string TenantId { get; }

    /// <summary>Signs in the personal Microsoft account named by the login hint.</summary>
    public Task SignInAsync(CancellationToken cancellationToken = default) =>
        AcquireTokenAsync([ManagementScope], interactive: true, cancellationToken);

    /// <summary>Gets an access token for Azure management or Speech data APIs.</summary>
    public ValueTask<AccessToken> GetTokenAsync(
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken = default) =>
        new(AcquireTokenAsync(scopes, interactive: false, cancellationToken));

    /// <summary>Removes this app's cached tokens.</summary>
    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _homeAccountId = null;
            if (_application is null)
                return;

            foreach (var account in await _application.GetAccountsAsync().ConfigureAwait(false))
                await _application.RemoveAsync(account).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    async Task<AccessToken> AcquireTokenAsync(
        IReadOnlyList<string> scopes,
        bool interactive,
        CancellationToken cancellationToken)
    {
        var effectiveScopes = scopes is { Count: > 0 }
            ? scopes
            : [SpeechScope];

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var application = GetApplication(cancellationToken);
            AuthenticationResult? result = null;
            if (!interactive)
            {
                var account = await FindPersonalAccountAsync(application, cancellationToken)
                    .ConfigureAwait(false);
                if (account is not null)
                {
                    try
                    {
                        result = await application
                            .AcquireTokenSilent(effectiveScopes, account)
                            .ExecuteAsync(cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (MsalUiRequiredException)
                    {
                        result = null;
                    }
                }
            }

            result ??= await AcquireInteractiveAsync(
                    application,
                    effectiveScopes,
                    cancellationToken)
                .ConfigureAwait(false);
            _homeAccountId = result.Account?.HomeAccountId?.Identifier;
            return new AccessToken(result.AccessToken, result.ExpiresOn);
        }
        finally
        {
            _gate.Release();
        }
    }

    async Task<IAccount?> FindPersonalAccountAsync(
        IPublicClientApplication application,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var accounts = await application.GetAccountsAsync().ConfigureAwait(false);
        if (!string.IsNullOrEmpty(_homeAccountId))
        {
            var selected = accounts.FirstOrDefault(account =>
                string.Equals(
                    account.HomeAccountId?.Identifier,
                    _homeAccountId,
                    StringComparison.Ordinal) &&
                IsPersonalAccount(account));
            if (selected is not null)
                return selected;
        }

        return accounts.FirstOrDefault(IsPersonalAccount);
    }

    bool IsPersonalAccount(IAccount account) =>
        account.Username?.Contains(_loginHint, StringComparison.OrdinalIgnoreCase) == true;

    IPublicClientApplication GetApplication(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_application is not null)
            return _application;

        var application = PublicClientApplicationBuilder
            .Create(ClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, TenantId)
            .WithDefaultRedirectUri()
            .WithParentActivityOrWindow(() => RequireActivity())
            .Build();

        _application = application;
        return application;
    }

    Task<AuthenticationResult> AcquireInteractiveAsync(
        IPublicClientApplication application,
        IEnumerable<string> scopes,
        CancellationToken cancellationToken)
    {
        var activity = RequireActivity();
        if (ReferenceEquals(Looper.MyLooper(), Looper.MainLooper))
            return AcquireOnUiThreadAsync(application, scopes, activity, cancellationToken);

        var completion = new TaskCompletionSource<AuthenticationResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        activity.RunOnUiThread(() =>
        {
            try
            {
                AcquireOnUiThreadAsync(application, scopes, activity, cancellationToken)
                    .ContinueWith(
                        task =>
                        {
                            if (task.IsCanceled)
                                completion.TrySetCanceled(cancellationToken);
                            else if (task.IsFaulted)
                                completion.TrySetException(
                                    task.Exception?.InnerExceptions ?? [task.Exception!]);
                            else
                                completion.TrySetResult(task.Result);
                        },
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        });
        return completion.Task;
    }

    Task<AuthenticationResult> AcquireOnUiThreadAsync(
        IPublicClientApplication application,
        IEnumerable<string> scopes,
        MainActivity activity,
        CancellationToken cancellationToken) =>
        application
            .AcquireTokenInteractive(scopes)
            .WithParentActivityOrWindow(activity)
            .WithLoginHint(_loginHint)
            .WithPrompt(Prompt.ForceLogin)
            .WithExtraQueryParameters(new Dictionary<string, (string, bool)>
            {
                ["domain_hint"] = ("consumers", false),
            })
            .WithUseEmbeddedWebView(true)
            .ExecuteAsync(cancellationToken);

    static MainActivity RequireActivity() =>
        MainActivity.Current
        ?? throw new InvalidOperationException(
            "Read Aloud must be visible before Azure sign-in can continue.");
}
