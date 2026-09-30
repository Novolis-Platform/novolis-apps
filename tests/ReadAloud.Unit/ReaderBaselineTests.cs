using Novolis.Avalonia.Speech;
using ReadAloud.Reading;
using ReadAloud.Services;

namespace ReadAloud.Unit;

public sealed class ReaderBaselineTests
{
    [Test]
    public async Task English_baselines_are_fifty_and_one_hundred_fifty_words()
    {
        await Assert.That(BaselinePassages.CountWords(BaselinePassages.FiftyWords)).IsEqualTo(50);
        await Assert.That(BaselinePassages.CountWords(BaselinePassages.OneHundredFiftyWords)).IsEqualTo(150);
    }

    [Test]
    public async Task Credential_summary_names_the_host_and_hides_the_key()
    {
        const string key = "super-secret-subscription-key";
        var summary = CredentialSummary.Describe(new AzureSpeechSetup
        {
            Endpoint = new Uri("https://eastus.api.cognitive.microsoft.com/"),
            CredentialSource = AzureSpeechCredentialSource.Manual,
            AuthenticationMode = AzureSpeechAuthenticationMode.ApiKey,
            ApiKey = key,
            VoiceName = "en-US-AvaMultilingualNeural",
            Locale = "en-US",
        });

        await Assert.That(summary).Contains("eastus.api.cognitive.microsoft.com");
        await Assert.That(summary).Contains("Subscription key saved");
        await Assert.That(summary).DoesNotContain(key);
    }

    [Test]
    public async Task Stop_returns_to_the_start_and_omits_the_spoken_text()
    {
        using var harness = SpeechServiceTests.CreateHarness();
        await harness.Speech.ConfigureAzureAsync(new AzureSpeechSetup
        {
            Endpoint = new Uri("https://speech.example.test/"),
            ApiKey = "super-secret-subscription-key",
            VoiceName = SpeechService.DefaultVoiceName,
        });
        harness.Speech.SynthesisOverride = (_, _) => Task.FromResult(new byte[] { 1, 2, 3 });
        harness.Player.BlockUntilCancelled = true;

        const string spoken = "alpha harbor light stays out of the report";
        var speaking = harness.Speech.SpeakAsync(spoken);
        var sawPlayback = false;
        for (var i = 0; i < 50 && harness.Player.PlayCount == 0; i++)
            await Task.Delay(20);
        sawPlayback = harness.Player.PlayCount > 0;
        harness.Speech.Stop();

        try
        {
            await speaking;
        }
        catch (OperationCanceledException)
        {
            // Stop cancels the listen.
        }

        await Assert.That(sawPlayback).IsTrue();
        await Assert.That(harness.Speech.Progress.Reading).IsFalse();
        await Assert.That(harness.Speech.Progress.Index).IsEqualTo(0);
        var report = harness.Speech.LastOperation;
        await Assert.That(report).IsNotNull();
        await Assert.That(report!.Phase).IsEqualTo("stopped");
        await Assert.That(report.IncludesSecret("super-secret-subscription-key")).IsFalse();
        await Assert.That(OperationReportText.Format(report)).DoesNotContain("alpha");
        await Assert.That(OperationReportText.Format(report)).DoesNotContain("super-secret-subscription-key");
    }
}
