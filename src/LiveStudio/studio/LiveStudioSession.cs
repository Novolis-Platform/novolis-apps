using Novolis.Audio.Live;
using Novolis.Audio.Live.Protocol;
using Novolis.Audio.Live.Protocol.Dto;
using Novolis.Audio.Live.Repl;
using Novolis.Audio.Live.Visuals;

namespace LiveStudio;

internal sealed class LiveStudioSession : IAsyncDisposable
{
    private readonly LiveHostProcess _host = new();
    private readonly LiveReplClient _client = new();
    private readonly SemaphoreSlim _clientGate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly IReadOnlyList<LiveProgramPreset> _presets = LiveSamplePrograms.CreateShowcasePresets();
    private readonly object _stateGate = new();
    private LiveGraphNode? _graph;
    private LiveTransportSnapshotDto? _snapshot;
    private IReadOnlyList<LiveDiagnosticDto> _diagnostics = [];
    private string _connectionStatus = "Starting local host...";
    private string _activityStatus = "Preparing the showcase.";
    private string _currentPresetName = "Waiting...";
    private string? _nextPresetName;
    private string? _errorMessage;
    private Task? _pollingTask;
    private Task? _showcaseTask;
    private bool _started;

    public event Action<LiveStudioState>? StateChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
            return;

        _started = true;

        PublishState();

        await _host.StartAsync(cancellationToken).ConfigureAwait(false);

        _connectionStatus = "Host online. Connecting over local IPC...";
        PublishState();

        await _client.ConnectAsync(LiveTransportEndpoints.CreateDefault(), cancellationToken).ConfigureAwait(false);

        _connectionStatus = "Connected to the live host.";
        _activityStatus = "Loading the first showcase preset.";
        PublishState();

        _pollingTask = PollSnapshotsAsync(_shutdown.Token);
        _showcaseTask = RunShowcaseAsync(_shutdown.Token);
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();

        if (_showcaseTask is not null)
        {
            try
            {
                await _showcaseTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

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

        await _client.DisposeAsync().ConfigureAwait(false);
        await _host.DisposeAsync().ConfigureAwait(false);
        _clientGate.Dispose();
        _shutdown.Dispose();
    }

    private async Task RunShowcaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            for (var i = 0; i < _presets.Count; i++)
            {
                var preset = _presets[i];
                if (preset.DelayBeforeCompile > TimeSpan.Zero)
                    await Task.Delay(preset.DelayBeforeCompile, cancellationToken).ConfigureAwait(false);

                await CompilePresetAsync(preset, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _activityStatus = "The showcase stopped unexpectedly.";
            _errorMessage = ex.Message;
            PublishState();
        }
    }

    private async Task CompilePresetAsync(LiveProgramPreset preset, CancellationToken cancellationToken)
    {
        try
        {
            var response = await SendAsync(
                token => _client.CompileAsync(preset.Definition, preset.SwapPolicy, token),
                cancellationToken).ConfigureAwait(false);

            _currentPresetName = preset.Name;
            var nextPreset = NextPresetAfter(preset);
            _nextPresetName = nextPreset?.Name;

            if (response.Success && response.Program is not null)
            {
                _activityStatus = response.Program.Version == preset.Version
                    ? $"Loaded {preset.Name} · swap {preset.SwapPolicy}."
                    : $"Loaded {preset.Name} as v{response.Program.Version} · swap {preset.SwapPolicy}.";

                _diagnostics = response.Diagnostics;

                lock (_stateGate)
                {
                    _graph = LiveVisualProjection.FromProgram(response.Program.ToDomain());
                }

                _errorMessage = null;
            }
            else
            {
                _activityStatus = $"Compile rejected for {preset.Name}.";
                _diagnostics = response.Diagnostics;
                _errorMessage = response.Diagnostics.Length > 0
                    ? string.Join(" ", response.Diagnostics.Select(d => $"{d.Code}: {d.Message}"))
                    : $"Compile rejected for {preset.Name}.";
            }

            await PublishSnapshotAsync(cancellationToken).ConfigureAwait(false);
            PublishState();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _activityStatus = $"Unable to compile {preset.Name}.";
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
                await PublishSnapshotAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task PublishSnapshotAsync(CancellationToken cancellationToken)
    {
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
            ConnectionStatus: _connectionStatus,
            ActivityStatus: _activityStatus,
            CurrentPresetName: _currentPresetName,
            NextPresetName: _nextPresetName,
            Snapshot: snapshot,
            Graph: graph,
            Diagnostics: _diagnostics,
            Presets: _presets,
            ErrorMessage: _errorMessage));
    }

    private LiveProgramPreset? NextPresetAfter(LiveProgramPreset preset)
    {
        for (var index = 0; index < _presets.Count - 1; index++)
        {
            if (ReferenceEquals(_presets[index], preset) || _presets[index].Name == preset.Name)
                return _presets[index + 1];
        }

        return null;
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
