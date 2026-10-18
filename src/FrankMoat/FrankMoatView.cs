using Avalonia.Controls;
using Avalonia.Input;
using FrankMoat.Agent;
using FrankMoat.Input;
using Novolis.Agent.Surface;
using Novolis.Avalonia.Gaming;
using Novolis.Avalonia.Rendering;

namespace FrankMoat;

internal sealed class FrankMoatView : UserControl, IDisposable
{
    private readonly GameShell _shell;
    private readonly PlanarSceneControl _viewport;
    private readonly FrankMoatSession _session;
    private readonly AgentSurface? _agentSurface;
    private bool _disposed;

    public event EventHandler? ExitRequested;

    public FrankMoatView(IGamepadReader? gamepad = null)
    {
        Focusable = true;
        var hud = new FrankMoatHud();
        _shell = GameShell.CreateWithPlanar(hud);
        _viewport = _shell.PlanarViewport
            ?? throw new InvalidOperationException("The game shell did not create a Planar viewport.");
        _session = new FrankMoatSession(_viewport, _shell, hud, gamepad);
        _session.ExitRequested += OnExitRequested;
        _agentSurface = AgentSurface.TryAttachFromEnvironment(
            new FrankMoatAgentHost(_session),
            FrankMoatAgentContract.Definition);

        Content = _shell;
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
        AttachedToVisualTree += (_, _) => _viewport.Focus();
    }

    public void FocusViewport() => _viewport.Focus();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        KeyDown -= OnKeyDown;
        KeyUp -= OnKeyUp;
        _session.ExitRequested -= OnExitRequested;
        _agentSurface?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _session.Dispose();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        _session.KeyDown(e.Key);
        if (e.Key is Key.W or Key.A or Key.S or Key.D or Key.C or Key.Escape)
        {
            e.Handled = true;
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        _session.KeyUp(e.Key);
        if (e.Key is Key.W or Key.A or Key.S or Key.D)
        {
            e.Handled = true;
        }
    }

    private void OnExitRequested(object? sender, EventArgs e) =>
        ExitRequested?.Invoke(this, e);
}
