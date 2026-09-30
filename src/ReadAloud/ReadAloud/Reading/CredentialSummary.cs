using Novolis.Avalonia.Speech;

namespace ReadAloud.Reading;

/// <summary>Shows that a Speech connection is saved without revealing the key.</summary>
public static class CredentialSummary
{
    public static string Describe(AzureSpeechSetup? setup)
    {
        if (setup is null)
            return "No Speech resource saved.";

        var host = setup.Endpoint.Host;
        var how = setup.EffectiveCredentialSource == AzureSpeechCredentialSource.Manual
            ? "Subscription key saved"
            : "Microsoft sign-in";
        return $"{host} · {how}";
    }
}
