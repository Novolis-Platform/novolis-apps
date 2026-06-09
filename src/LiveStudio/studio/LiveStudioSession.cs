using LiveStudio.Shared.Launcher;
using Novolis.Audio.Live;
using Novolis.Audio.Live.Protocol;
using Novolis.Audio.Live.Protocol.Dto;
using Novolis.Audio.Live.Repl;
using Novolis.Audio.Live.Visuals;

namespace LiveStudio;

internal sealed class LiveStudioSession : IAsyncDisposable
{
    private readonly LiveLauncherClient _launcher = new();
    private LiveReplClient _client = new();
    private readonly SemaphoreSlim _clientGate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly IReadOnlyList<LiveProgramPreset> _presets = LiveSamplePrograms.CreateShowcasePresets();
    private readonly object _stateGate = new();
    private LiveGraphNode? _graph;
    private LiveTransportSnapshotDto? _snapshot;
    private IReadOnlyList<LiveDiagnosticDto> _diagnostics = [];
    private string _launcherStatus = "Connecting to launcher...";
    private string _connectionStatus = "Waiting for launcher...";
    private string _activityStatus = "Write live code and compile to the host.";
    private string _currentPresetName = "No program loaded";
    private string? _nextPresetName;
    private string? _errorMessage;
    private bool _hasFatalLauncherError;
    private int _lastLauncherRestartCount;
    private Task? _pollingTask;
    private Task? _reconnectTask;
    private bool _started;

    public event Action<LiveStudioState>? StateChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
            return;

        _started = true;
        PublishState();

        _launcher.StatusChanged += OnLauncherStatusChanged;
        await _launcher.ConnectAsync(cancellationToken).ConfigureAwait(false);

        var launcherStatus = await _launcher.WaitForHostReadyAsync(cancellationToken).ConfigureAwait(false);
        _launcherStatus = launcherStatus.Message;
        _lastLauncherRestartCount = launcherStatus.RestartCount;
        _connectionStatus = "Host online. Connecting over local IPC...";
        PublishState();

        await ConnectToHostAsync(cancellationToken).ConfigureAwait(false);

        _connectionStatus = "Connected to the live host.";
        _activityStatus = "Ready. Press Compile to send code to the host.";
        PublishState();

        _pollingTask = PollSnapshotsAsync(_shutdown.Token);
    }

    public async Task CompileSourceAsync(string source, SwapPolicy swapPolicy, CancellationToken cancellationToken = default)
    {
        if (_hasFatalLauncherError)
            throw new InvalidOperationException(_errorMessage ?? "The launcher reported a fatal host error.");

        try
        {
            _activityStatus = "Compiling live code on the host...";
            PublishState();

            var response = await SendAsync(
                token => _client.CompileTextAsync(source, swapPolicy, token),
                cancellationToken).ConfigureAwait(false);

            _currentPresetName = "Live buffer";
            _nextPresetName = null;

            if (response.Success && response.Program is not null)
            {
                _activityStatus = $"Compiled live code as v{response.Program.Version} · swap {swapPolicy}.";
                _diagnostics = response.Diagnostics;

                lock (_stateGate)
                {
                    _graph = LiveVisualProjection.FromProgram(response.Program.ToDomain());
                }

                _errorMessage = null;
            }
            else
            {
                _activityStatus = "Compile rejected.";
                _diagnostics = response.Diagnostics;
                _errorMessage = response.Diagnostics.Length > 0
                    ? string.Join(" ", response.Diagnostics.Select(d => $"{d.Code}: {d.Message}"))
                    : "Compile rejected by the live host.";
            }

            await PublishSnapshotAsync(cancellationToken).ConfigureAwait(false);
            PublishState();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _activityStatus = "Compile failed.";
            _errorMessage = ex.Message;
            PublishState();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        _launcher.StatusChanged -= OnLauncherStatusChanged;

        if (_pollingTask is not null)
        {
            try
            {
                await _pollingTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (_reconnectTask is not null)
        {
            try
            {
                await _reconnectTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        await _client.DisposeAsync().ConfigureAwait(false);
        await _launcher.DisposeAsync().ConfigureAwait(false);
        _clientGate.Dispose();
        _shutdown.Dispose();
    }

    private void OnLauncherStatusChanged(LiveLauncherStatus status)
    {
        _launcherStatus = status.Message;

        if (status.IsFatal)
        {
            _hasFatalLauncherError = true;
            _errorMessage = status.Message;
            _connectionStatus = "Launcher stopped the host.";
            _activityStatus = "The studio cannot continue because the host could not be recovered.";
        }
        else if (status.State == LiveLauncherState.Restarting)
        {
            _connectionStatus = "Host is restarting...";
            _activityStatus = status.Message;
            _errorMessage = null;
        }
        else if (status.IsHostReady)
        {
            _connectionStatus = _client.IsConnected ? "Connected to the live host." : "Host is ready.";
            _errorMessage = null;
            _hasFatalLauncherError = false;

            if (status.RestartCount > _lastLauncherRestartCount)
            {
                _lastLauncherRestartCount = status.RestartCount;
                _reconnectTask = ReconnectToHostAsync(_shutdown.Token);
            }
        }

        PublishState();
    }

    private async Task ConnectToHostAsync(CancellationToken cancellationToken)
    {
        if (!_client.IsConnected)
            await _client.ConnectAsync(LiveTransportEndpoints.CreateDefault(), cancellationToken).ConfigureAwait(false);
    }

    private async Task ReconnectToHostAsync(CancellationToken cancellationToken)
    {
        try
        {
            _connectionStatus = "Reconnecting to restarted host...";
            _activityStatus = "Waiting for the relaunched host IPC endpoint.";
            PublishState();

            await _client.DisposeAsync().ConfigureAwait(false);
            _client = new LiveReplClient();
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            await ConnectToHostAsync(cancellationToken).ConfigureAwait(false);

            _connectionStatus = "Connected to the live host.";
            _activityStatus = "Host recovered. Recompile to resume playback.";
            _errorMessage = null;
            PublishState();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _connectionStatus = "Unable to reconnect to the host.";
            _activityStatus = ex.Message;
            _errorMessage = ex.Message;
            PublishState();
        }
    }

    private async Task PollSnapshotsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));

            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_hasFatalLauncherError)
                    break;

                await PublishSnapshotAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task PublishSnapshotAsync(CancellationToken cancellationToken)
    {
        if (!_client.IsConnected)
            return;

        try
        {
            var snapshot = await SendAsync(token => _client.SnapshotAsync(token), cancellationToken).ConfigureAwait(false);

            lock (_stateGate)
            {
                _snapshot = snapshot;
            }

            PublishState();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
            _activityStatus = "Lost contact with the live host.";
            PublishState();
        }
    }

    private void PublishState()
    {
        LiveGraphNode? graph;
        LiveTransportSnapshotDto? snapshot;

        lock (_stateGate)
        {
            graph = _graph;
            snapshot = _snapshot;
        }

        StateChanged?.Invoke(new LiveStudioState(
            LauncherStatus: _launcherStatus,
            ConnectionStatus: _connectionStatus,
            ActivityStatus: _activityStatus,
            CurrentPresetName: _currentPresetName,
            NextPresetName: _nextPresetName,
            Snapshot: snapshot,
            Graph: graph,
            Diagnostics: _diagnostics,
            Presets: _presets,
            ErrorMessage: _errorMessage,
            HasFatalLauncherError: _hasFatalLauncherError));
    }

    private async ValueTask<T> SendAsync<T>(Func<CancellationToken, ValueTask<T>> action, CancellationToken cancellationToken)
    {
        await _clientGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await action(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _clientGate.Release();
        }
    }
}
