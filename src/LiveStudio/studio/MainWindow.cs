using Avalonia.Controls;
using Avalonia.Threading;
using LiveStudio.Components;

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
        _session.EditorDocumentRequested += source => _workspace.SetEditorDocument(source);
        _workspace.CompileRequested += OnCompileRequested;
        _workspace.DemoRequested += OnDemoRequested;
        _workspace.PresetSelected += OnPresetSelected;
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        try
        {
            await Program.Runtime.EnsureStartedAsync().ConfigureAwait(false);
        }
        catch
        {
            // Session.ReportStartupFailure already published UI state.
            Dispatcher.UIThread.Post(() => Title = "Novolis Audio Live Studio — host unavailable");
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
            System.Diagnostics.Debug.WriteLine($"Compile request failed: {ex}");
        }
    }

    private async void OnDemoRequested(object? sender, EventArgs e)
    {
        try
        {
            await _session.ReplayDemoAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Demo request failed: {ex}");
        }
    }

    private async void OnPresetSelected(LiveProgramPreset preset)
    {
        try
        {
            await _session.LoadPresetAsync(preset).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Preset load failed: {ex}");
        }
    }

    private void OnStateChanged(LiveStudioState state)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _workspace.Bind(state);

            if (state.HasFatalLauncherError)
                Title = "Novolis Audio Live Studio — host unavailable";
            else if (state.Snapshot?.ActiveProgramId is not null)
                Title = $"Novolis Audio Live — {state.CurrentPresetName} @ {state.Snapshot.Bpm:0} BPM";
            else if (state.IsHostConnected)
                Title = "Novolis Audio Live Studio";
        });
    }
}
