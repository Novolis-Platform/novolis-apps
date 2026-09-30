namespace ReadAloud.Reading;

/// <summary>Asks the running reader to speak the baselines from a credential file.</summary>
public static class BaselineLaunch
{
    static readonly object Gate = new();
    static string? _pending;

    public static event Action<string>? Requested;

    public static void Request(string credentialPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialPath);
        Action<string>? handler;
        lock (Gate)
        {
            handler = Requested;
            if (handler is null)
                _pending = credentialPath;
        }

        handler?.Invoke(credentialPath);
    }

    public static bool TryTake(out string credentialPath)
    {
        lock (Gate)
        {
            credentialPath = _pending ?? "";
            _pending = null;
            return credentialPath.Length > 0;
        }
    }
}
