using BooksWriterStudio.Services;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Audio.Voice;
using Novolis.Avalonia.Mobile;
using Novolis.Avalonia.Speech;
using Novolis.Manuscript.Export.Audio;

namespace BooksWriterStudio.Unit;

[NotInParallel]
public sealed class CompositionRootTests
{
    [Test]
    public async Task Startup_graph_resolves_when_azure_speech_is_absent()
    {
        using var env = AzureSpeechEnvironment.Clear();
        var services = new ServiceCollection();
        services.AddSingleton<IVoiceService, SilentVoice>();
        services.AddSingleton<ISecureTokenStore, MemoryTokenStore>();
        Program.AddWriterStudioServices(services);
        using var provider = services.BuildServiceProvider();

        var front = provider.GetRequiredService<SpeechFront>();
        await WriterSpeechStartup.InitializeAsync(front);

        await Assert.That(provider.GetRequiredService<WriterSession>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<WriterSettingsStore>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<SpellService>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<ISynthesizer>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<SpeechPreview>()).IsNotNull();
        await Assert.That(front.IsAzureConfigured).IsFalse();
        await Assert.That(front.Capabilities.CanCreateMp3).IsFalse();
        await Assert.That(provider.GetRequiredService<ISynthesizer>() is SpeechFrontSynthesizer)
            .IsTrue();
    }

    sealed class SilentVoice : IVoiceService
    {
        public Task SpeakAsync(string text, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WriteToFileAsync(
            string text,
            FileInfo destination,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    sealed class MemoryTokenStore : ISecureTokenStore
    {
        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
