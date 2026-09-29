using System.Text.Json;
using Novolis.Avalonia.Speech;
using ReadAloud.Services;

namespace ReadAloud.Android;

/// <summary>
/// Loads a developer-only Speech subscription key from Android app-private
/// storage. The file is never packaged into the APK.
/// </summary>
public sealed class AndroidAzureSpeechFallbackProvider : IAzureSpeechFallbackProvider
{
    const string FileName = "azure-speech-fallback.json";
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public AndroidAzureSpeechFallbackProvider()
    {
        var filesDirectory = global::Android.App.Application.Context?.FilesDir?.AbsolutePath
            ?? throw new InvalidOperationException(
                "Android application files directory is unavailable.");
        FilePath = Path.Combine(filesDirectory, FileName);
    }

    /// <summary>Private app-data path used for local developer setup.</summary>
    public string FilePath { get; }

    public bool IsAvailable => File.Exists(FilePath);

    public async Task<AzureSpeechSetup> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath))
        {
            throw new InvalidOperationException(
                "No local Azure Speech fallback file is installed.");
        }

        AzureSpeechFallbackDocument? document;
        try
        {
            await using var stream = new FileStream(
                FilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);
            document = await JsonSerializer.DeserializeAsync<AzureSpeechFallbackDocument>(
                    stream,
                    JsonOptions,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "The local Azure Speech fallback file is not valid JSON.",
                ex);
        }

        if (document is null ||
            !Uri.TryCreate(document.Endpoint, UriKind.Absolute, out var endpoint) ||
            !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(document.SubscriptionKey))
        {
            throw new InvalidOperationException(
                "The local Azure Speech fallback needs an HTTPS endpoint and subscription key.");
        }

        return new AzureSpeechSetup
        {
            Endpoint = endpoint,
            AuthenticationMode = AzureSpeechAuthenticationMode.ApiKey,
            ApiKey = document.SubscriptionKey,
            VoiceName = string.IsNullOrWhiteSpace(document.VoiceName)
                ? "en-US-AvaMultilingualNeural"
                : document.VoiceName,
            Locale = string.IsNullOrWhiteSpace(document.Locale)
                ? "en-US"
                : document.Locale,
        };
    }

    sealed class AzureSpeechFallbackDocument
    {
        public string? Endpoint { get; set; }

        public string? SubscriptionKey { get; set; }

        public string? VoiceName { get; set; }

        public string? Locale { get; set; }
    }
}
