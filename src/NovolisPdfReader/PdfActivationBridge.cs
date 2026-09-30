using Novolis.Pdf.Platform;

namespace NovolisPdfReader;

/// <summary>Provides a process-wide activation handoff before the UI is ready.</summary>
public static class PdfActivationBridge
{
    private static PdfActivationInbox? _inbox;

    /// <summary>Connects the bridge to the application's activation inbox.</summary>
    public static void Initialize(PdfActivationInbox inbox) =>
        _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));

    /// <summary>Publishes an activation when the host has started.</summary>
    public static bool Publish(PdfOpenRequest request) =>
        _inbox?.Publish(request) == true;
}
