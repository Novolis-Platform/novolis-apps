using System.Diagnostics;
using ReadAloud.Services;

namespace ReadAloud.Reading;

/// <summary>
/// Speaks the 50-word and 150-word English baselines through the same
/// <see cref="SpeechService"/> the reader uses.
/// </summary>
public static class BaselineRun
{
    public static async Task<string> ExecuteAsync(
        SpeechService speech,
        string credentialPath,
        Action<string>? showPassage = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(speech);
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialPath);

        var fiftyWords = BaselinePassages.CountWords(BaselinePassages.FiftyWords);
        var oneFiftyWords = BaselinePassages.CountWords(BaselinePassages.OneHundredFiftyWords);
        if (fiftyWords != 50 || oneFiftyWords != 150)
        {
            throw new InvalidOperationException(
                $"Baseline passages must be 50 and 150 words, got {fiftyWords} and {oneFiftyWords}.");
        }

        await using var stream = File.OpenRead(credentialPath);
        var setup = await AzureSpeechCredentialFile.ReadAsync(stream, cancellationToken)
            .ConfigureAwait(false);
        await speech.ConfigureAzureAsync(setup, cancellationToken).ConfigureAwait(false);

        var started = Stopwatch.GetTimestamp();
        string line;
        try
        {
            showPassage?.Invoke(BaselinePassages.FiftyWords);
            await speech.SpeakAsync(BaselinePassages.FiftyWords, cancellationToken).ConfigureAwait(false);
            showPassage?.Invoke(BaselinePassages.OneHundredFiftyWords);
            await speech.SpeakAsync(BaselinePassages.OneHundredFiftyWords, cancellationToken)
                .ConfigureAwait(false);
            var elapsed = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var report = speech.LastOperation;
            var tail = report is null
                ? "no-report"
                : $"phase={report.Phase} characters={report.Characters} azure={report.AzureCalls} cache={report.CacheReplays}";
            line = $"READALOUD_BASELINE ok words=50/150 elapsedMs={elapsed} {tail}";
        }
        catch (Exception ex)
        {
            line = $"READALOUD_BASELINE failed type={ex.GetType().Name}";
            await WriteResultAsync(credentialPath, line, CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        await WriteResultAsync(credentialPath, line, cancellationToken).ConfigureAwait(false);
        return line;
    }

    static async Task WriteResultAsync(
        string credentialPath,
        string line,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(credentialPath);
        if (string.IsNullOrWhiteSpace(directory))
            return;

        var destination = Path.Combine(directory, "baseline-result.txt");
        await File.WriteAllTextAsync(destination, line, cancellationToken).ConfigureAwait(false);
    }
}
