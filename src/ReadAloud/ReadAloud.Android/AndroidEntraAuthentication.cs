using Azure.Core;
using Microsoft.Identity.Client;

namespace ReadAloud.Android;

/// <summary>
/// Single-tenant public-client authentication for Android. MSAL owns the
/// browser flow and the platform-native secure token cache.
/// </summary>
public sealed class AndroidEntraAuthentication
{
    public const string ManagementScope = "https://management.azure.com/.default";
    public const string SpeechScope = "https://cognitiveservices.azure.com/.default";

    readonly SemaphoreSlim _gate = new(1, 1);
    IPublicClientApplication? _application;

    public AndroidEntraAuthentication(
        string clientId,
        string tenantId)
    {
        ClientId = string.IsNullOrWhiteSpace(clientId)
            ? throw new ArgumentException("Client id is required.", nameof(clientId))
            : clientId;
        TenantId = string.IsNullOrWhiteSpace(tenantId)
            ? throw new ArgumentException("Tenant id is required.", nameof(tenantId))
            : tenantId;
    }

    public string ClientId { get; }

    public string TenantId { get; }

    /// <summary>Signs in and validates management-plane access.</summary>
    public async Task SignInAsync(CancellationToken cancellationToken = default)
    {
        await GetTokenAsync([ManagementScope], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gets an access token for Azure management or Speech data APIs.</summary>
    public async ValueTask<AccessToken> GetTokenAsync(
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken = default)
    {
        var effectiveScopes = scopes is { Count: > 0 }
            ? scopes
            : [SpeechScope];

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var application = GetApplication(cancellationToken);
            var accounts = await application.GetAccountsAsync().ConfigureAwait(false);
            var account = accounts.FirstOrDefault();

            AuthenticationResult result;
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
                    result = await AcquireInteractiveAsync(
                            application,
                            effectiveScopes,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            else
            {
                result = await AcquireInteractiveAsync(
                        application,
                        effectiveScopes,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return new AccessToken(result.AccessToken, result.ExpiresOn);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Removes all cached accounts and their encrypted token state.</summary>
    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_application is not null)
            {
                foreach (var account in await _application.GetAccountsAsync().ConfigureAwait(false))
                    await _application.RemoveAsync(account).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    IPublicClientApplication GetApplication(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_application is not null)
            return _application;

        var application = PublicClientApplicationBuilder
            .Create(ClientId)
            .WithAuthority($"https://login.microsoftonline.com/{TenantId}")
            .WithDefaultRedirectUri()
            .WithParentActivityOrWindow(() =>
                MainActivity.Current
                ?? throw new InvalidOperationException(
                    "Read Aloud must be visible before Azure sign-in can continue."))
            .Build();

        _application = application;
        return application;
    }

    static Task<AuthenticationResult> AcquireInteractiveAsync(
        IPublicClientApplication application,
        IEnumerable<string> scopes,
        CancellationToken cancellationToken)
    {
        var activity = MainActivity.Current
            ?? throw new InvalidOperationException(
                "Read Aloud must be visible before Azure sign-in can continue.");
        return application
            .AcquireTokenInteractive(scopes)
            .WithParentActivityOrWindow(activity)
            .ExecuteAsync(cancellationToken);
    }
}
