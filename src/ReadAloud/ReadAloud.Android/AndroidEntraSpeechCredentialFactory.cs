using Azure.Core;
using Novolis.Avalonia.Speech;

namespace ReadAloud.Android;

/// <summary>Creates Android-native Microsoft Entra credentials for Azure Speech.</summary>
public sealed class AndroidEntraSpeechCredentialFactory : IAzureSpeechCredentialFactory
{
    readonly AndroidEntraAuthentication _authentication;

    public AndroidEntraSpeechCredentialFactory(AndroidEntraAuthentication authentication)
    {
        _authentication = authentication
            ?? throw new ArgumentNullException(nameof(authentication));
    }

    public TokenCredential Create(AzureSpeechSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        if (!string.Equals(
                setup.ClientId,
                _authentication.ClientId,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The selected Azure Speech resource is not configured for this app.");
        }

        return new AndroidEntraSpeechCredential(_authentication);
    }
}

sealed class AndroidEntraSpeechCredential : TokenCredential
{
    readonly AndroidEntraAuthentication _authentication;

    public AndroidEntraSpeechCredential(AndroidEntraAuthentication authentication) =>
        _authentication = authentication
            ?? throw new ArgumentNullException(nameof(authentication));

    public override AccessToken GetToken(
        TokenRequestContext requestContext,
        CancellationToken cancellationToken) =>
        GetTokenAsync(requestContext, cancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();

    public override ValueTask<AccessToken> GetTokenAsync(
        TokenRequestContext requestContext,
        CancellationToken cancellationToken) =>
        _authentication.GetTokenAsync(requestContext.Scopes, cancellationToken);
}
