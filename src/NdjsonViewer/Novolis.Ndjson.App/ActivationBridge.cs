namespace Novolis.Ndjson.App;

public static class ActivationBridge
{
    private static readonly object Gate = new();
    private static readonly Queue<NdjsonOpenRequest> Pending = new();
    private static DocumentActivationInbox? _inbox;

    public static void Initialize(DocumentActivationInbox inbox)
    {
        ArgumentNullException.ThrowIfNull(inbox);
        lock (Gate)
        {
            _inbox = inbox;
            while (Pending.TryDequeue(out var request))
                inbox.Publish(request);
        }
    }

    public static void Publish(NdjsonOpenRequest request)
    {
        lock (Gate)
        {
            if (_inbox is null)
                Pending.Enqueue(request);
            else
                _inbox.Publish(request);
        }
    }
}
