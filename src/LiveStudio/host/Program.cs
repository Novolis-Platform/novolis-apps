using Novolis.Audio.Live;
using Novolis.Audio.Live.Protocol;
using Novolis.Audio.Live.Protocol.Dto;
using Novolis.Audio.Live.Render;
using Novolis.Transports.LocalIpc;

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdown.Cancel();
};
AppDomain.CurrentDomain.ProcessExit += (_, _) => shutdown.Cancel();

var endpoint = LiveTransportEndpoints.CreateDefault();
await using var listener = LocalIpcTransport.CreateListener(endpoint);
var session = new LiveSession();
await using var engine = new OscillatorLiveAudioEngine();
engine.Bind(session);
await engine.StartAsync(shutdown.Token);

Console.WriteLine($"Novolis Audio Live host listening on {endpoint.Kind} {endpoint.Address} (oscillator render on; audio clock)");

while (!shutdown.IsCancellationRequested)
{
    try
    {
        var connection = await listener.AcceptAsync(shutdown.Token);
        _ = Task.Run(() => HandleConnectionAsync(connection, session, shutdown.Token), shutdown.Token);
    }
    catch (OperationCanceledException)
    {
        break;
    }
}

await engine.StopAsync();

static async Task HandleConnectionAsync(ILocalIpcConnection connection, LiveSession session, CancellationToken cancellationToken)
{
    await using (connection)
    {
        await foreach (var frame in connection.ReadAllAsync(cancellationToken))
        {
            switch (frame.Name)
            {
                case LiveRpcMethodNames.Compile:
                    await HandleCompileAsync(connection, frame, session);
                    break;
                case LiveRpcMethodNames.Snapshot:
                    await HandleSnapshotAsync(connection, frame, session);
                    break;
                case LiveRpcMethodNames.QueueSwap:
                    await HandleQueueSwapAsync(connection, frame, session);
                    break;
                default:
                    break;
            }
        }
    }
}

static async Task HandleCompileAsync(ILocalIpcConnection connection, LocalIpcFrame frame, LiveSession session)
{
    var request = LiveProtocolCodec.Deserialize<LiveCompileRequestDto>(frame.Payload);
    var definition = request.Program.ToDomain();
    var result = session.Submit(definition, request.SwapPolicy);

    var response = new LiveCompileResponseDto(
        request.RequestId,
        result.Success,
        result.Program is null ? null : result.Program.ToDto(),
        result.Diagnostics.Select(d => d.ToDto()).ToArray());

    await connection.SendMessageAsync(
        frame.Sequence,
        LiveRpcMessageKinds.Response,
        LiveRpcMethodNames.Compile,
        response);
}

static async Task HandleSnapshotAsync(ILocalIpcConnection connection, LocalIpcFrame frame, LiveSession session)
{
    var request = LiveProtocolCodec.Deserialize<LiveSnapshotRequestDto>(frame.Payload);
    var response = new LiveSnapshotResponseDto(request.RequestId, session.CreateSnapshot().ToDto());

    await connection.SendMessageAsync(
        frame.Sequence,
        LiveRpcMessageKinds.Response,
        LiveRpcMethodNames.Snapshot,
        response);
}

static async Task HandleQueueSwapAsync(ILocalIpcConnection connection, LocalIpcFrame frame, LiveSession session)
{
    var request = LiveProtocolCodec.Deserialize<LiveQueueSwapRequestDto>(frame.Payload);
    var queued = session.TryQueueSwap(request.ProgramId, request.SwapPolicy);
    var response = new LiveQueueSwapResponseDto(
        request.RequestId,
        queued,
        queued ? [] : [new LiveDiagnosticDto("LIVE010", $"Program '{request.ProgramId}' was not found.", LiveDiagnosticSeverity.Error, null)]);

    await connection.SendMessageAsync(
        frame.Sequence,
        LiveRpcMessageKinds.Response,
        LiveRpcMethodNames.QueueSwap,
        response);
}
