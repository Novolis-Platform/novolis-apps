using System.Text.Json;

namespace ReadAloud.Services;

/// <summary>
/// Counts speech activity on this device. Azure Monitor is a separate 30-day
/// resource total; this ledger is what the app itself sent or replayed.
/// </summary>
public sealed class SpeechUsageLedger
{
    readonly string _path;
    readonly object _gate = new();
    long _charactersSent;
    long _azureCalls;
    long _cacheReplays;
    long _failures;
    string? _lastFailure;

    public SpeechUsageLedger(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        Load();
    }

    public void RecordAzureCall(int characters)
    {
        if (characters < 0)
            characters = 0;
        lock (_gate)
        {
            _charactersSent += characters;
            _azureCalls++;
            Save();
        }
    }

    public void RecordCacheReplay()
    {
        lock (_gate)
        {
            _cacheReplays++;
            Save();
        }
    }

    public void RecordFailure(string reason)
    {
        var safe = string.IsNullOrWhiteSpace(reason) ? "failed" : reason.Trim();
        if (safe.Length > 120)
            safe = safe[..120];
        lock (_gate)
        {
            _failures++;
            _lastFailure = safe;
            Save();
        }
    }

    public string Format()
    {
        lock (_gate)
        {
            var text =
                $"This device: {_charactersSent:N0} characters sent, " +
                $"{_azureCalls:N0} Azure calls, " +
                $"{_cacheReplays:N0} cache replays, " +
                $"{_failures:N0} failures";
            if (_failures > 0 && !string.IsNullOrWhiteSpace(_lastFailure))
                text += $" (last: {_lastFailure})";
            return text + ".";
        }
    }

    void Load()
    {
        try
        {
            if (!File.Exists(_path))
                return;
            var document = JsonSerializer.Deserialize<LedgerFile>(File.ReadAllText(_path));
            if (document is null)
                return;
            _charactersSent = Math.Max(0, document.CharactersSent);
            _azureCalls = Math.Max(0, document.AzureCalls);
            _cacheReplays = Math.Max(0, document.CacheReplays);
            _failures = Math.Max(0, document.Failures);
            _lastFailure = document.LastFailure;
        }
        catch
        {
            // A damaged ledger starts again. Speech still works.
        }
    }

    void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            var json = JsonSerializer.Serialize(new LedgerFile
            {
                CharactersSent = _charactersSent,
                AzureCalls = _azureCalls,
                CacheReplays = _cacheReplays,
                Failures = _failures,
                LastFailure = _lastFailure,
            });
            File.WriteAllText(_path, json);
        }
        catch
        {
            // Usage display is best-effort. Playback must not fail because the ledger could not be saved.
        }
    }

    sealed class LedgerFile
    {
        public long CharactersSent { get; set; }
        public long AzureCalls { get; set; }
        public long CacheReplays { get; set; }
        public long Failures { get; set; }
        public string? LastFailure { get; set; }
    }
}
