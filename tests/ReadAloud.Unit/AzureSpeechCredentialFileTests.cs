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

    [Test]
    public async Task Encrypted_bundle_round_trips_manual_credentials_without_plaintext_key()
    {
        var setup = AzureSpeechCredentialFile.FromManualEntry(
            "https://speech.example.test/",
            "secret-key",
            "en-US-JennyNeural",
            "en-US");
        await using var output = new MemoryStream();

        await AzureSpeechCredentialBundle.WriteAsync(output, setup, "correct horse");
        var json = System.Text.Encoding.UTF8.GetString(output.ToArray());
        output.Position = 0;
        var restored = await AzureSpeechCredentialBundle.ReadAsync(output, "correct horse");

        await Assert.That(json.Contains("secret-key", StringComparison.Ordinal)).IsFalse();
        await Assert.That(restored.Endpoint).IsEqualTo(setup.Endpoint);
        await Assert.That(restored.ApiKey).IsEqualTo("secret-key");
        await Assert.That(restored.VoiceName).IsEqualTo("en-US-JennyNeural");
    }

    [Test]
    public async Task Encrypted_bundle_rejects_the_wrong_passphrase()
    {
        var setup = AzureSpeechCredentialFile.FromManualEntry(
            "https://speech.example.test/",
            "secret-key");
        await using var output = new MemoryStream();
        await AzureSpeechCredentialBundle.WriteAsync(output, setup, "correct horse");
        output.Position = 0;

        await Assert.That(async () =>
                await AzureSpeechCredentialBundle.ReadAsync(output, "wrong pass"))
            .ThrowsExactly<InvalidOperationException>();
    }
}
