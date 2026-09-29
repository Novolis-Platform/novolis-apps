using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Avalonia.Speech;

namespace ReadAloud.Services;

/// <summary>Reads the deterministic manual Azure Speech credential file format.</summary>
public static class AzureSpeechCredentialFile
{
    /// <summary>Stable schema identifier for Read Aloud credential files.</summary>
    public const string Schema = "novolis.readaloud.azure-speech-credentials";

    /// <summary>Current credential file schema version.</summary>
    public const int Version = 1;

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>Loads and validates a manual credential file from an opened stream.</summary>
    public static async Task<AzureSpeechSetup> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        AzureSpeechCredentialDocument? document;
        try
        {
            document = await JsonSerializer.DeserializeAsync<AzureSpeechCredentialDocument>(
                    stream,
                    JsonOptions,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "The selected Azure credentials file is not valid JSON.",
                ex);
        }

        if (document is null)
            throw new InvalidOperationException("The Azure credentials file is empty.");
        if (!string.Equals(document.Schema, Schema, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The Azure credentials file must declare schema '{Schema}'.");
        }
        if (document.Version != Version)
        {
            throw new InvalidOperationException(
                $"The Azure credentials file version must be {Version}.");
        }
        if (!string.Equals(
                document.Authentication,
                "subscriptionKey",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The Azure credentials file authentication must be 'subscriptionKey'.");
        }
        if (!Uri.TryCreate(document.Endpoint, UriKind.Absolute, out var endpoint) ||
            !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The Azure credentials file needs an absolute HTTPS endpoint.");
        }
        if (string.IsNullOrWhiteSpace(document.SubscriptionKey))
        {
            throw new InvalidOperationException(
                "The Azure credentials file needs a subscriptionKey.");
        }

        return new AzureSpeechSetup
        {
            Endpoint = endpoint,
            CredentialSource = AzureSpeechCredentialSource.Manual,
            AuthenticationMode = AzureSpeechAuthenticationMode.ApiKey,
            ApiKey = document.SubscriptionKey,
            VoiceName = string.IsNullOrWhiteSpace(document.VoiceName)
                ? "en-US-AvaMultilingualNeural"
                : document.VoiceName.Trim(),
            Locale = string.IsNullOrWhiteSpace(document.Locale)
                ? "en-US"
                : document.Locale.Trim(),
        };
    }

    sealed class AzureSpeechCredentialDocument
    {
        [JsonPropertyName("schema")]
        public string? Schema { get; set; }

        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("authentication")]
        public string? Authentication { get; set; }

        [JsonPropertyName("endpoint")]
        public string? Endpoint { get; set; }

        [JsonPropertyName("subscriptionKey")]
        public string? SubscriptionKey { get; set; }

        [JsonPropertyName("voiceName")]
        public string? VoiceName { get; set; }

        [JsonPropertyName("locale")]
        public string? Locale { get; set; }
    }
}
