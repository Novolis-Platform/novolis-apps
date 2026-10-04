namespace Novolis.Reach.Client;

internal static class ReachClientSends
{
    internal static Task ClipboardText(
        ReachClientSession session,
        string text,
        CancellationToken cancellationToken) =>
        session.SendAsync(
            ReachMessageType.ClipboardContent,
            new ReachClipboardContent("text", text),
            cancellationToken);

    internal static Task PointerMove(
        ReachClientSession session,
        double x,
        double y,
        CancellationToken cancellationToken) =>
        session.SendAsync(
            ReachMessageType.PointerMove,
            new ReachPointerMove(x, y),
            cancellationToken);

    internal static Task PointerButton(
        ReachClientSession session,
        string button,
        bool isDown,
        int clickCount,
        CancellationToken cancellationToken) =>
        session.SendAsync(
            ReachMessageType.PointerButton,
            new ReachPointerButton(button, isDown, clickCount),
            cancellationToken);

    internal static Task PointerWheel(
        ReachClientSession session,
        int delta,
        CancellationToken cancellationToken) =>
        session.SendAsync(
            ReachMessageType.PointerWheel,
            new ReachPointerWheel(delta),
            cancellationToken);

    internal static Task Key(
        ReachClientSession session,
        ushort virtualKey,
        bool isDown,
        CancellationToken cancellationToken) =>
        session.SendAsync(
            isDown ? ReachMessageType.KeyDown : ReachMessageType.KeyUp,
            new ReachKeyEvent(virtualKey),
            cancellationToken);

    internal static Task Text(
        ReachClientSession session,
        string text,
        CancellationToken cancellationToken) =>
        session.SendAsync(
            ReachMessageType.TextInput,
            new ReachTextInput(text),
            cancellationToken);

    internal static Task ClipboardFiles(
        ReachClientSession session,
        IEnumerable<string> files,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        return session.SendAsync(
            ReachMessageType.ClipboardContent,
            new ReachClipboardContent("files", null, files.ToArray()),
            cancellationToken);
    }

    internal static Task ConfigureVideo(
        ReachClientSession session,
        int width,
        int height,
        int framesPerSecond,
        int targetBitrate,
        CancellationToken cancellationToken) =>
        session.SendAsync(
            ReachMessageType.VideoStreamConfiguration,
            new ReachVideoStreamConfiguration(
                "H264",
                width,
                height,
                framesPerSecond,
                targetBitrate),
            cancellationToken);

    internal static Task SelectDisplay(
        ReachClientSession session,
        string displayId,
        CancellationToken cancellationToken) =>
        session.SendAsync(
            ReachMessageType.DisplaySelect,
            new ReachDisplaySelect(displayId),
            cancellationToken);

    internal static async Task RequestKeyFrame(
        ReachClientSession session,
        CancellationToken cancellationToken)
    {
        session._performance.RecordKeyFrameRequest();
        session.RaisePerformanceChanged();
        await session.SendAsync(
                ReachMessageType.RequestKeyFrame,
                new ReachRequestKeyFrame(session._lastVideoSequence),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
