using Novolis.Pdf.Platform;

namespace NovolisPdfReader;

/// <summary>Provides a process-wide activation handoff before the UI is ready.</summary>
public static class PdfActivationBridge
{
    private static readonly object Gate = new();
    private static readonly Queue<PdfOpenRequest> Pending = new();
    private static PdfActivationInbox? _inbox;

    /// <summary>Connects the bridge to the application's activation inbox.</summary>
    public static void Initialize(PdfActivationInbox inbox)
    {
        ArgumentNullException.ThrowIfNull(inbox);
        lock (Gate)
        {
            _inbox = inbox;
            while (Pending.TryDequeue(out var request))
                inbox.Publish(request);
        }
    }

    /// <summary>Publishes an activation immediately or queues it until startup.</summary>
    public static bool Publish(PdfOpenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (Gate)
        {
            if (_inbox is null)
            {
                Pending.Enqueue(request);
                return true;
            }

            return _inbox.Publish(request);
        }
    }
}
