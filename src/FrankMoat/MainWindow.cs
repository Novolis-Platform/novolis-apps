using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using FrankMoat.Input;
using Novolis.Avalonia.GraphicalProfile;

namespace FrankMoat;

internal sealed class MainWindow : Window
{
    private readonly FrankMoatView _view;

    public MainWindow(IGamepadReader? gamepad = null)
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

        _view = new FrankMoatView(gamepad);

        Content = _view;
        _view.ExitRequested += OnExitRequested;
        Closed += (_, _) => _view.Dispose();
        Opened += (_, _) => _view.FocusViewport();
    }

    private void OnExitRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(Close);
}
