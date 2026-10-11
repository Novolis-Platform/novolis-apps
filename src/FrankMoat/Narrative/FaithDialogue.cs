namespace FrankMoat.Narrative;

internal sealed class FaithDialogueQueue
{
    private readonly PriorityQueue<FaithLine, int> _pending = new();
    private FaithLine? _current;

    public string Text => _current?.Text ?? string.Empty;

    public string Speaker => _current?.Speaker ?? string.Empty;

    public bool IsInstruction => _current?.IsInstruction == true;

    public float RemainingSeconds => _current?.RemainingSeconds ?? 0f;

    public void Reset()
    {
        _pending.Clear();
        _current = null;
    }

    public void Say(
        string text,
        float seconds = 4f,
        int priority = 50,
        bool instruction = false)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _pending.Enqueue(
            new FaithLine("FAITH", text, seconds, instruction),
            -priority);
        StartNext();
    }

    public void Tick(float dt, bool combat)
    {
        if (_current is { IsInstruction: false } && combat)
        {
            return;
        }

        if (_current is null)
        {
            StartNext();
            return;
        }

        _current = _current with { RemainingSeconds = _current.RemainingSeconds - dt };
        if (_current.RemainingSeconds <= 0f)
        {
            _current = null;
            StartNext();
        }
    }

    public void Dismiss()
    {
        _current = null;
        StartNext();
    }

    private void StartNext()
    {
        if (_current is null && _pending.TryDequeue(out var next, out _))
        {
            _current = next;
        }
    }
}
