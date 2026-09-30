using Novolis.Avalonia.Speech;
using ReadAloud.Services;

namespace ReadAloud.Unit;

public sealed class AzureSpeechCredentialFileTests
{
    [Test]
    public async Task From_manual_entry_accepts_https_endpoint_and_key()
    {
        var setup = AzureSpeechCredentialFile.FromManualEntry(
            "https://speech.example.test/",
            "secret-key",
            "en-US-JennyNeural",
            "en-US");

        await Assert.That(setup.Endpoint.AbsoluteUri)
            .IsEqualTo("https://speech.example.test/");
        await Assert.That(setup.ApiKey).IsEqualTo("secret-key");
        await Assert.That(setup.VoiceName).IsEqualTo("en-US-JennyNeural");
        await Assert.That(setup.Locale).IsEqualTo("en-US");
        await Assert.That(setup.EffectiveCredentialSource)
            .IsEqualTo(AzureSpeechCredentialSource.Manual);
        await Assert.That(setup.AuthenticationMode)
            .IsEqualTo(AzureSpeechAuthenticationMode.ApiKey);
    }

    [Test]
    public async Task From_manual_entry_defaults_voice_to_ava()
    {
        var setup = AzureSpeechCredentialFile.FromManualEntry(
            "https://speech.example.test/",
            "secret-key");

        await Assert.That(setup.VoiceName).IsEqualTo(SpeechService.DefaultVoiceName);
        await Assert.That(setup.Locale).IsEqualTo(SpeechService.DefaultLocale);
    }

    [Test]
    public async Task From_manual_entry_rejects_non_https_endpoint()
    {
        await Assert.That(() =>
                AzureSpeechCredentialFile.FromManualEntry(
                    "http://speech.example.test/",
                    "secret-key"))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task From_manual_entry_rejects_empty_key()
    {
        await Assert.That(() =>
                AzureSpeechCredentialFile.FromManualEntry(
                    "https://speech.example.test/",
                    "  "))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Read_uses_fallback_voice_when_file_omits_it()
    {
        var json =
            """
            {
              "schema": "novolis.readaloud.azure-speech-credentials",
              "version": 1,
              "authentication": "subscriptionKey",
              "endpoint": "https://speech.example.test/",
              "subscriptionKey": "secret-key"
            }
            """;

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var setup = await AzureSpeechCredentialFile.ReadAsync(
            stream,
            "en-US-JennyNeural",
            "en-US");

        await Assert.That(setup.VoiceName).IsEqualTo("en-US-JennyNeural");
        await Assert.That(setup.Locale).IsEqualTo("en-US");
        await Assert.That(setup.ApiKey).IsEqualTo("secret-key");
    }
}
