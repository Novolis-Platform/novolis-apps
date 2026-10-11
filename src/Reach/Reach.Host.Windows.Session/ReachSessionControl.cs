using Novolis.Transports.Framing;
using Novolis.Transports.LocalIpc;
using Novolis.Windows.Input;

namespace Novolis.Reach.Host.Windows.Session;

internal sealed class ReachSessionControl(ReachSessionHost host)
{
    internal async Task HandleAsync(
        LocalIpcFrame frame,
        CancellationToken cancellationToken)
    {
        var envelope = ReachMessageCodec.Deserialize(frame.Payload);
        switch (envelope.Type)
        {
            case ReachMessageType.SessionOpen:
            {
                var open = ReachMessageCodec.ReadBody<ReachSessionOpen>(envelope);
                host._selectedDisplayId = "display-0";
                host.SelectDisplay(open.RequestedDisplayId);
                host._audioEnabled = open.EnableAudio;
                if (host._capture is null)
                    await host.Capture.StartAsync(cancellationToken).ConfigureAwait(false);
                else
                    await host.Capture.SendMetadataAsync(cancellationToken).ConfigureAwait(false);
                break;
            }
            case ReachMessageType.SessionResume:
            {
                var resume = ReachMessageCodec.ReadBody<ReachSessionResume>(envelope);
                host._audioEnabled = resume.EnableAudio;
                if (host._capture is null)
                    await host.Capture.StartAsync(cancellationToken).ConfigureAwait(false);
                else
                    await host.Capture.SendMetadataAsync(cancellationToken).ConfigureAwait(false);
                break;
            }
            case ReachMessageType.SessionClose:
                await host.Capture.StopAsync().ConfigureAwait(false);
                break;
            case ReachMessageType.PointerMove:
            {
                var move = ReachMessageCodec.ReadBody<ReachPointerMove>(envelope);
                host._input.MovePointer(
                    (int)global::System.Math.Round(move.X),
                    (int)global::System.Math.Round(move.Y));
                break;
            }
            case ReachMessageType.PointerButton:
            {
                var button = ReachMessageCodec.ReadBody<ReachPointerButton>(envelope);
                if (Enum.TryParse<WindowsInputController.WindowsPointerButton>(
                        button.Button,
                        ignoreCase: true,
                        out var parsedButton))
                {
                    host._input.Button(parsedButton, button.IsDown);
                }

                break;
            }
            case ReachMessageType.PointerWheel:
                host._input.Scroll(
                    ReachMessageCodec.ReadBody<ReachPointerWheel>(envelope).Delta);
                break;
            case ReachMessageType.KeyDown:
                host._input.Key(
                    ReachMessageCodec.ReadBody<ReachKeyEvent>(envelope).VirtualKey,
                    release: false);
                break;
            case ReachMessageType.KeyUp:
                host._input.Key(
                    ReachMessageCodec.ReadBody<ReachKeyEvent>(envelope).VirtualKey,
                    release: true);
                break;
            case ReachMessageType.TextInput:
                host._input.Text(ReachMessageCodec.ReadBody<ReachTextInput>(envelope).Text);
                break;
            case ReachMessageType.ClipboardContent:
                await ApplyClipboardAsync(
                        ReachMessageCodec.ReadBody<ReachClipboardContent>(envelope),
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            case ReachMessageType.ClipboardChanged:
            {
                var request = ReachMessageCodec.ReadBody<ReachClipboardChanged>(envelope);
                await SendClipboardAsync(request.Format, cancellationToken)
                    .ConfigureAwait(false);
                break;
            }
            case ReachMessageType.VideoStreamConfiguration:
            {
                var configuration = ReachMessageCodec.ReadBody<ReachVideoStreamConfiguration>(
                    envelope);
                if (!string.Equals(
                        configuration.Codec,
                        "H264",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"Reach host does not support {configuration.Codec} video.");
                }

                await host.Capture.RestartAsync(
                        global::System.Math.Clamp(configuration.Width, 0, 3840),
                        global::System.Math.Clamp(configuration.Height, 0, 2160),
                        global::System.Math.Clamp(configuration.FramesPerSecond, 5, 60),
                        global::System.Math.Clamp(
                            configuration.TargetBitrate,
                            250_000,
                            50_000_000),
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            }
            case ReachMessageType.DisplayResize:
            {
                var resize = ReachMessageCodec.ReadBody<ReachDisplayResize>(envelope);
                await host.Capture.RestartAsync(
                        global::System.Math.Clamp(resize.Width, 0, 3840),
                        global::System.Math.Clamp(resize.Height, 0, 2160),
                        global::System.Math.Clamp(resize.FramesPerSecond, 5, 60),
                        host._targetBitrate,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            }
            case ReachMessageType.DisplaySelect:
            {
                if (!host.SelectDisplay(
                        ReachMessageCodec.ReadBody<ReachDisplaySelect>(envelope).DisplayId))
                {
                    break;
                }

                if (host._capture is not null)
                {
                    await host.Capture.RestartAsync(
                            host._targetWidth,
                            host._targetHeight,
                            host._framesPerSecond,
                            host._targetBitrate,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                break;
            }
            case ReachMessageType.RequestKeyFrame:
                await host.Capture.RestartAsync(
                        host._targetWidth,
                        host._targetHeight,
                        host._framesPerSecond,
                        host._targetBitrate,
                        cancellationToken,
                        notifyReset: false)
                    .ConfigureAwait(false);
                break;
        }
    }

    private async Task ApplyClipboardAsync(
        ReachClipboardContent clipboard,
        CancellationToken cancellationToken)
    {
        if (string.Equals(clipboard.Format, "text", StringComparison.OrdinalIgnoreCase)
            && clipboard.Text is not null)
        {
            await host.Clipboard.WriteTextAsync(
                    clipboard.Text,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (string.Equals(clipboard.Format, "files", StringComparison.OrdinalIgnoreCase)
                 && clipboard.Files is not null)
        {
            await host.Clipboard.WriteFileDropListAsync(
                    clipboard.Files,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task SendClipboardAsync(
        string requestedFormat,
        CancellationToken cancellationToken)
    {
        var connection = host._connection;
        if (connection is null)
            return;

        var supportsFiles = string.Equals(
            requestedFormat,
            "files",
            StringComparison.OrdinalIgnoreCase);
        var files = supportsFiles
            ? await host.Clipboard.ReadFileDropListAsync(cancellationToken)
                .ConfigureAwait(false)
            : Array.Empty<string>();
        var text = await host.Clipboard.ReadTextAsync(cancellationToken)
            .ConfigureAwait(false);
        await host.SendAsync(
                connection,
                ReachMessageType.ClipboardContent,
                new ReachClipboardContent(
                    files.Count == 0 ? "text" : "files",
                    text,
                    files.ToArray()),
                "control",
                cancellationToken)
            .ConfigureAwait(false);
    }
}
