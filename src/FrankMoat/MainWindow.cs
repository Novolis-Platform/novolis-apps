using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Novolis.Avalonia.Gaming;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Avalonia.Rendering;

namespace FrankMoat;

internal sealed class MainWindow : Window
{
    private readonly GameShell _shell;
    private readonly PlanarSceneControl _viewport;
    private readonly FrankMoatHud _hud;
    private readonly FrankMoatSession _session;

    public MainWindow()
    {
        Title = "Frank Moat vs. the Evil Undead";
        Width = 1440;
        Height = 900;
        MinWidth = 960;
        MinHeight = 600;
        Focusable = true;

        GraphicalProfileBinding.Bind(
            this,
            Window.BackgroundProperty,
            GraphicalProfile.BackgroundResourceKey);
        GraphicalProfileBinding.Bind(
            this,
            Window.ForegroundProperty,
            GraphicalProfile.TextResourceKey);

        _hud = new FrankMoatHud();
        _shell = GameShell.CreateWithPlanar(_hud);
        _viewport = _shell.PlanarViewport
            ?? throw new InvalidOperationException("The game shell did not create a Planar viewport.");
        _session = new FrankMoatSession(_viewport, _shell, _hud);

        Content = _shell;
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
        Closed += (_, _) => _session.Dispose();
        Opened += (_, _) => _viewport.Focus();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        _session.KeyDown(e.Key);
        if (e.Key is Key.W or Key.A or Key.S or Key.D or Key.Escape)
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
}
