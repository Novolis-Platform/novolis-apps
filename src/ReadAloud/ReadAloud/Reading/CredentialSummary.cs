using Novolis.Avalonia.Speech;

namespace ReadAloud.Reading;

/// <summary>Shows that a Speech connection is saved without revealing the key.</summary>
public static class CredentialSummary
{
    public static string Describe(AzureSpeechSetup? setup)
    {
        if (setup is null)
            return "Not connected";

        var host = setup.Endpoint.Host;
        var how = setup.EffectiveCredentialSource == AzureSpeechCredentialSource.Manual
            ? "Endpoint & key"
            : "Microsoft account";
        return $"{host} · {how}";
    }

    public static string Title(AzureSpeechSetup? setup) =>
        setup is null ? "Connect Azure Speech" : "Speech ready";

    public static string Detail(AzureSpeechSetup? setup)
    {
        if (setup is null)
            return "Sign in with Microsoft or enter a Speech endpoint and key.";

        return setup.EffectiveCredentialSource == AzureSpeechCredentialSource.Manual
            ? $"{setup.Endpoint.Host} · using a saved subscription key"
            : $"{setup.Endpoint.Host} · signed in with Microsoft";
    }
}
