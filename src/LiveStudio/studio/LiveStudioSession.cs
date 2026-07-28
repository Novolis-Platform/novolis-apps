using LiveStudio.Shared.Hosting;
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
    private LiveHostProcess? _ownedHost;
    private LiveGraphNode? _graph;
    private LiveTransportSnapshotDto? _snapshot;
    private IReadOnlyList<LiveDiagnosticDto> _diagnostics = [];
    private string _launcherStatus = "Connecting to launcher...";
    private string _connectionStatus = "Waiting for launcher...";
    private string _activityStatus = "Starting the live demo...";
    private string _currentPresetName = "No program loaded";
    private string? _nextPresetName;
    private string? _errorMessage;
    private bool _hasFatalLauncherError;
    private int _lastLauncherRestartCount;
    private Task? _pollingTask;
    private Task? _reconnectTask;
    private Task? _showcaseTask;
    private CancellationTokenSource? _showcaseCts;
    private int _showcaseGeneration;
    private bool _started;
    private bool _demoSequenceRunning;

    public event Action<LiveStudioState>? StateChanged;

    public IReadOnlyList<LiveProgramPreset> Presets => _presets;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
            return;

        _started = true;
        PublishState();

        try
        {
            await EnsureLauncherOrLocalHostAsync(cancellationToken).ConfigureAwait(false);

            _connectionStatus = "Connecting to live host IPC...";
            PublishState();

            await ConnectToHostWithRetryAsync(cancellationToken).ConfigureAwait(false);

            _connectionStatus = "Connected to the live host.";
            _activityStatus = "Loading Pulse Bloom — you should hear audio in a moment.";
            _errorMessage = null;
            _hasFatalLauncherError = false;
            PublishState();

            _pollingTask = PollSnapshotsAsync(_shutdown.Token);
            _showcaseTask = RunShowcaseAsync(_shutdown.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || _shutdown.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ReportStartupFailure(ex);
            throw;
        }
    }

    public void ReportStartupFailure(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        CancelShowcase();
        _hasFatalLauncherError = true;
        _launcherStatus = "Could not reach the live host.";
        _connectionStatus = "Not connected.";
        _activityStatus = "Close other Live windows, then run the Launcher profile.";
        _errorMessage = CompactError(ex.Message);
        _currentPresetName = "No program loaded";
        _nextPresetName = null;
        PublishState();
    }

    public Task CompileSourceAsync(string source, SwapPolicy swapPolicy, CancellationToken cancellationToken = default)
    {
        CancelShowcase();
        return CompileTextCoreAsync(LiveReplSource.Normalize(source), swapPolicy, cancellationToken);
    }

    public Task LoadPresetAsync(LiveProgramPreset preset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preset);
        CancelShowcase();
        return CompilePresetAsync(preset, cancellationToken);
    }

    public Task ReplayDemoAsync(CancellationToken cancellationToken = default)
    {
        CancelShowcase();
        _showcaseTask = RunShowcaseAsync(cancellationToken);
        return _showcaseTask;
    }

    public async ValueTask DisposeAsync()
    {
        CancelShowcase();
        _shutdown.Cancel();
        _launcher.StatusChanged -= OnLauncherStatusChanged;

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
        if (_ownedHost is not null)
            await _ownedHost.DisposeAsync().ConfigureAwait(false);
        _clientGate.Dispose();
        _shutdown.Dispose();
        _showcaseCts?.Dispose();
    }

    private async Task EnsureLauncherOrLocalHostAsync(CancellationToken cancellationToken)
    {
        _launcher.StatusChanged += OnLauncherStatusChanged;

        var launcherOk = false;
        try
        {
            _connectionStatus = "Connecting to launcher...";
            PublishState();

            await _launcher.ConnectAsync(cancellationToken).ConfigureAwait(false);

            _connectionStatus = "Waiting for host IPC...";
            PublishState();

            var launcherStatus = await _launcher.WaitForHostReadyAsync(cancellationToken).ConfigureAwait(false);
            _launcherStatus = launcherStatus.Message;
            _lastLauncherRestartCount = launcherStatus.RestartCount;
            PublishState();
            launcherOk = true;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && !_shutdown.IsCancellationRequested)
        {
            // Includes TaskCanceledException from the launcher connect timeout — that means
            // "no launcher", not "studio is shutting down".
            _launcherStatus = $"Launcher unavailable: {CompactError(ex.Message)}";
            _connectionStatus = "Starting a local live host...";
            _activityStatus = "No launcher — launching host from the studio process.";
            PublishState();
        }

        if (launcherOk)
            return;

        cancellationToken.ThrowIfCancellationRequested();

        if (await LiveHostEndpoint.IsListeningAsync(cancellationToken).ConfigureAwait(false))
        {
            _launcherStatus = "Using an already-running live host.";
            PublishState();
            return;
        }

        _ownedHost = new LiveHostProcess();
        await _ownedHost.StartAsync(cancellationToken).ConfigureAwait(false);
        _launcherStatus = "Local live host started by studio.";
        _connectionStatus = "Waiting for local host IPC...";
        PublishState();

        await LiveHostEndpoint.WaitUntilListeningAsync(
            LiveLauncherEndpoints.HostReadyTimeout,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task ConnectToHostWithRetryAsync(CancellationToken cancellationToken)
    {
        await LiveHostEndpoint.WaitUntilListeningAsync(
            LiveLauncherEndpoints.HostReadyTimeout,
            cancellationToken).ConfigureAwait(false);

        Exception? lastError = null;
        for (var attempt = 1; attempt <= 8; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await ConnectToHostAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex;
                _connectionStatus = $"Host IPC retry {attempt}/8...";
                _errorMessage = CompactError(ex.Message);
                PublishState();
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken).ConfigureAwait(false);

                await _client.DisposeAsync().ConfigureAwait(false);
                _client = new LiveReplClient();
            }
        }

        throw new InvalidOperationException(
            $"Unable to connect to the live host IPC endpoint after retries. {lastError?.Message}");
    }

    private void CancelShowcase()
    {
        Interlocked.Increment(ref _showcaseGeneration);
        _demoSequenceRunning = false;
        try
        {
            _showcaseCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task RunShowcaseAsync(CancellationToken linkedToken)
    {
        var generation = Interlocked.Increment(ref _showcaseGeneration);
        _showcaseCts?.Dispose();
        _showcaseCts = CancellationTokenSource.CreateLinkedTokenSource(linkedToken, _shutdown.Token);
        var cancellationToken = _showcaseCts.Token;
        _demoSequenceRunning = true;
        PublishState();

        try
        {
            for (var i = 0; i < _presets.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var preset = _presets[i];
                if (preset.DelayBeforeCompile > TimeSpan.Zero)
                    await Task.Delay(preset.DelayBeforeCompile, cancellationToken).ConfigureAwait(false);

                await CompilePresetAsync(preset, cancellationToken).ConfigureAwait(false);
            }

            if (Volatile.Read(ref _showcaseGeneration) == generation && _demoSequenceRunning)
            {
                _activityStatus = "Demo sequence finished. Click a preset or edit Note.Play and press F5 / Ctrl+Enter.";
                _nextPresetName = null;
                PublishState();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (Volatile.Read(ref _showcaseGeneration) == generation)
            {
                _activityStatus = "The showcase stopped unexpectedly.";
                _errorMessage = CompactError(ex.Message);
                PublishState();
            }
        }
        finally
        {
            if (Volatile.Read(ref _showcaseGeneration) == generation)
            {
                _demoSequenceRunning = false;
                PublishState();
            }
        }
    }

    private async Task CompileTextCoreAsync(string source, SwapPolicy swapPolicy, CancellationToken cancellationToken)
    {
        if (_hasFatalLauncherError)
        {
            _activityStatus = "Host unavailable.";
            _errorMessage = _errorMessage ?? "The launcher reported a fatal host error.";
            PublishState();
            return;
        }

        if (!_client.IsConnected)
        {
            _activityStatus = "Not connected yet.";
            _errorMessage = "Wait for the host, or run: dotnet run --project novolis-apps/src/LiveStudio/launcher/LiveStudio.Launcher.csproj";
            PublishState();
            return;
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            _activityStatus = "Empty buffer.";
            _errorMessage = "Type Note.Play(C4); then press F5.";
            PublishState();
            return;
        }

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
                _errorMessage = CompactError(response.Diagnostics.Length > 0
                    ? string.Join(" ", response.Diagnostics.Select(d => $"{d.Code}: {d.Message}"))
                    : "Compile rejected by the live host.");
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
            _errorMessage = CompactError(ex.Message);
            PublishState();
        }
    }

    private async Task CompilePresetAsync(LiveProgramPreset preset, CancellationToken cancellationToken)
    {
        if (_hasFatalLauncherError || !_client.IsConnected)
        {
            _activityStatus = "Host unavailable.";
            _errorMessage = _errorMessage
                ?? "Connect to the live host before loading a preset.";
            PublishState();
            return;
        }

        try
        {
            _activityStatus = $"Loading {preset.Name}...";
            PublishState();

            var response = await SendAsync(
                token => _client.CompileAsync(preset.Definition, preset.SwapPolicy, token),
                cancellationToken).ConfigureAwait(false);

            _currentPresetName = preset.Name;
            _nextPresetName = _demoSequenceRunning ? NextPresetAfter(preset)?.Name : null;

            if (response.Success && response.Program is not null)
            {
                _activityStatus = $"Playing {preset.Name} as v{response.Program.Version} · swap {preset.SwapPolicy}.";
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
                _errorMessage = CompactError(response.Diagnostics.Length > 0
                    ? string.Join(" ", response.Diagnostics.Select(d => $"{d.Code}: {d.Message}"))
                    : $"Compile rejected for {preset.Name}.");
            }

            await PublishSnapshotAsync(cancellationToken).ConfigureAwait(false);
            PublishState();
        }
        catch (OperationCanceledException)
        {
            // Showcase cancellation or shutdown — ignore.
        }
        catch (Exception ex)
        {
            _activityStatus = $"Unable to compile {preset.Name}.";
            _errorMessage = CompactError(ex.Message);
            PublishState();
        }
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
        if (_client.IsConnected)
            return;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await _client.ConnectAsync(LiveTransportEndpoints.CreateDefault(), timeout.Token).ConfigureAwait(false);
    }

    private async Task ReconnectToHostAsync(CancellationToken cancellationToken)
    {
        try
        {
            CancelShowcase();
            _connectionStatus = "Reconnecting to restarted host...";
            _activityStatus = "Waiting for the relaunched host IPC endpoint.";
            PublishState();

            await _client.DisposeAsync().ConfigureAwait(false);
            _client = new LiveReplClient();
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            await ConnectToHostAsync(cancellationToken).ConfigureAwait(false);

            _connectionStatus = "Connected to the live host.";
            _activityStatus = "Host recovered — replaying Pulse Bloom.";
            _errorMessage = null;
            PublishState();

            if (_presets.Count > 0)
                await CompilePresetAsync(_presets[0], cancellationToken).ConfigureAwait(false);
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
            HasFatalLauncherError: _hasFatalLauncherError,
            DemoSequenceRunning: _demoSequenceRunning,
            IsHostConnected: _client.IsConnected && !_hasFatalLauncherError));
    }

    private static string CompactError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Unknown error.";

        var trimmed = message.Trim();
        const int max = 180;
        if (trimmed.Length <= max)
            return trimmed;

        return trimmed[..max].TrimEnd() + "…";
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
