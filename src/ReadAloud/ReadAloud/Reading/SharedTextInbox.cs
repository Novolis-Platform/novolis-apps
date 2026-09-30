namespace ReadAloud.Reading;

/// <summary>Text another app shared into Read Aloud.</summary>
public static class SharedTextInbox
{
    static readonly object Gate = new();
    static string? _pending;

    public static event Action<string>? Received;

    public static void Publish(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        text = text.Trim();
        Action<string>? handler;
        lock (Gate)
        {
            handler = Received;
            if (handler is null)
                _pending = text;
        }

        handler?.Invoke(text);
    }

    public static bool TryTake(out string text)
    {
        lock (Gate)
        {
            text = _pending ?? "";
            _pending = null;
            return text.Length > 0;
        }
    }
}
