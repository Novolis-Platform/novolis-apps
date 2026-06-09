using Avalonia.Controls;
using Avalonia.Threading;
using Novolis.Audio.Live.Protocol.Dto;

namespace LiveStudio;

internal sealed class MainWindow : Window
{
    private readonly LiveStudioDashboard _dashboard = new();
    private readonly LiveStudioSession _session;

    public MainWindow(LiveStudioSession session)
    {
        _session = session;
        Title = "Novolis Audio Live Studio";
        Width = 1360;
        Height = 900;
        MinWidth = 1100;
        MinHeight = 760;
        Content = _dashboard;

        _session.StateChanged += OnStateChanged;
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        try
        {
            await Program.Launcher.EnsureStartedAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var state = new LiveStudioState(
                    ConnectionStatus: "Host failed to start.",
                    ActivityStatus: "The studio could not reach the live host.",
                    CurrentPresetName: "Unavailable",
                    NextPresetName: null,
                    Snapshot: new LiveTransportSnapshotDto(null, null, 0m, 0m, 1, 1, null, null, ex.Message),
                    Graph: null,
                    Diagnostics: [],
                    Presets: LiveSamplePrograms.CreateShowcasePresets(),
                    ErrorMessage: ex.Message);

                _dashboard.Bind(state);
            });
        }
    }

    private void OnStateChanged(LiveStudioState state) =>
        Dispatcher.UIThread.Post(() => _dashboard.Bind(state));
}
