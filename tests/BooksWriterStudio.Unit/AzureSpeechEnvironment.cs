namespace BooksWriterStudio.Unit;

sealed class AzureSpeechEnvironment : IDisposable
{
    readonly string? _endpoint;
    readonly string? _key;

    AzureSpeechEnvironment(string? endpoint, string? key)
    {
        _endpoint = Environment.GetEnvironmentVariable("NOVOLIS_AZURE_SPEECH_ENDPOINT");
        _key = Environment.GetEnvironmentVariable("NOVOLIS_AZURE_SPEECH_KEY");
        Environment.SetEnvironmentVariable("NOVOLIS_AZURE_SPEECH_ENDPOINT", endpoint);
        Environment.SetEnvironmentVariable("NOVOLIS_AZURE_SPEECH_KEY", key);
    }

    public static AzureSpeechEnvironment Clear() => new(null, null);

    public static AzureSpeechEnvironment Set(string endpoint, string key) => new(endpoint, key);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("NOVOLIS_AZURE_SPEECH_ENDPOINT", _endpoint);
        Environment.SetEnvironmentVariable("NOVOLIS_AZURE_SPEECH_KEY", _key);
    }
}
