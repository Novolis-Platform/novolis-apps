using Avalonia.Controls;
using Avalonia.Threading;
using LiveStudio.Components;
using Novolis.Audio.Live.Protocol.Dto;

namespace LiveStudio;

internal sealed class MainWindow : Window
{
    private readonly LiveCodingWorkspace _workspace = new();
    private readonly LiveStudioSession _session;

    public MainWindow(LiveStudioSession session)
    {
        _session = session;
        Title = "Novolis Audio Live Studio";
        Width = 1480;
        Height = 920;
        MinWidth = 1180;
        MinHeight = 760;
        Content = _workspace;

        _session.StateChanged += OnStateChanged;
        _workspace.CompileRequested += OnCompileRequested;
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        try
        {
            await Program.Runtime.EnsureStartedAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var state = new LiveStudioState(
                    LauncherStatus: "Launcher unavailable.",
                    ConnectionStatus: "Could not reach the live host.",
                    ActivityStatus: "Start the launcher before opening the studio.",
                    CurrentPresetName: "Unavailable",
                    NextPresetName: null,
                    Snapshot: new LiveTransportSnapshotDto(null, null, 0m, 0m, 1, 1, null, null, ex.Message),
                    Graph: null,
                    Diagnostics: [],
                    Presets: LiveSamplePrograms.CreateShowcasePresets(),
                    ErrorMessage: ex.Message,
                    HasFatalLauncherError: true);

                _workspace.Bind(state);
            });
        }
    }

    private async void OnCompileRequested(object? sender, EventArgs e)
    {
        try
        {
            await _session.CompileSourceAsync(
                _workspace.SourceText,
                _workspace.SelectedSwapPolicy).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var state = new LiveStudioState(
                    LauncherStatus: "Compile failed.",
                    ConnectionStatus: "Compile failed.",
                    ActivityStatus: ex.Message,
                    CurrentPresetName: "Unavailable",
                    NextPresetName: null,
                    Snapshot: null,
                    Graph: null,
                    Diagnostics: [],
                    Presets: LiveSamplePrograms.CreateShowcasePresets(),
                    ErrorMessage: ex.Message,
                    HasFatalLauncherError: true);

                _workspace.Bind(state);
            });
        }
    }

    private void OnStateChanged(LiveStudioState state)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _workspace.Bind(state);

            if (state.HasFatalLauncherError)
            {
                Title = "Novolis Audio Live Studio — host stopped";
                Close();
            }
        });
    }
}
