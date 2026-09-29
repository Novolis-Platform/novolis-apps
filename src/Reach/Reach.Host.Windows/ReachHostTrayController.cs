using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Novolis.Reach.Protocol;

namespace Novolis.Reach.Host.Windows;

/// <summary>Owns the per-user Reach host notification-area companion.</summary>
public sealed class ReachHostTrayController : IDisposable
{
    private Window? _window;
    private TrayIcon? _tray;
    private readonly ReachHostClient _client;
    private NativeMenuItem? _pause;
    private NativeMenuItem? _reconnect;
    private NativeMenuItem? _stop;
    private ReachHostStatus? _lastStatus;
    private bool _forceExit;

    /// <summary>Creates the notification-area controller.</summary>
    public ReachHostTrayController(ReachHostClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>Attaches the tray companion to the host dashboard window.</summary>
    public void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (_window is not null)
            return;

        _window = window;
        _window.Closing += OnClosing;
        _window.PropertyChanged += OnWindowPropertyChanged;
        EnsureTray();
    }

    /// <summary>Hides the dashboard while leaving the host running.</summary>
    public void HideToTray()
    {
        if (_window is null)
            return;

        EnsureTray();
        _window.Hide();
    }

    /// <summary>Shows and activates the host dashboard.</summary>
    public void ShowWindow()
    {
        if (_window is null)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            _window.Show();
            _window.WindowState = WindowState.Normal;
            _window.Activate();
        });
    }

    /// <summary>Updates the tray tooltip and action state from service health.</summary>
    public void UpdateStatus(ReachHostStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        _lastStatus = status;
        void Apply()
        {
            if (_tray is not null)
            {
                _tray.ToolTipText =
                    $"Novolis Reach Host — {status.State}; "
                    + $"{status.ConnectedClients} client(s)";
            }

            if (_pause is not null)
            {
                _pause.Header = status.SharingPaused
                    ? "Resume sharing"
                    : "Pause sharing";
                _pause.IsEnabled = !string.Equals(
                    status.State,
                    "Stopped by operator",
                    StringComparison.Ordinal);
            }

            if (_reconnect is not null)
                _reconnect.IsEnabled = !string.Equals(
                    status.State,
                    "Stopped by operator",
                    StringComparison.Ordinal);
            if (_stop is not null)
                _stop.IsEnabled = !string.Equals(
                    status.State,
                    "Stopped by operator",
                    StringComparison.Ordinal);
        }

        if (Dispatcher.UIThread.CheckAccess())
            Apply();
        else
            Dispatcher.UIThread.Post(Apply);
    }

    /// <summary>Exits the per-user companion process.</summary>
    public void ExitApplication()
    {
        _forceExit = true;
        if (_window is not null)
        {
            Dispatcher.UIThread.Post(() => _window.Close());
            return;
        }

        if (Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_window is not null)
        {
            _window.Closing -= OnClosing;
            _window.PropertyChanged -= OnWindowPropertyChanged;
        }

        if (_tray is not null)
        {
            _tray.IsVisible = false;
            _tray.Dispose();
            _tray = null;
        }

        if (Application.Current is { } app)
            TrayIcon.SetIcons(app, null);
    }

    private void OnClosing(object? sender, WindowClosingEventArgs args)
    {
        if (_forceExit)
            return;

        args.Cancel = true;
        HideToTray();
    }

    private void OnWindowPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property != Window.WindowStateProperty
            || _window?.WindowState != WindowState.Minimized)
        {
            return;
        }

        HideToTray();
    }

    private void EnsureTray()
    {
        if (_tray is not null || Application.Current is null)
            return;

        WindowIcon? icon = _window?.Icon;
        if (icon is null)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "icon.png");
            if (File.Exists(path))
            {
                using var stream = File.OpenRead(path);
                icon = new WindowIcon(stream);
            }
        }

        var menu = new NativeMenu();
        var open = new NativeMenuItem("Open Novolis Reach Host");
        open.Click += (_, _) => ShowWindow();
        _pause = new NativeMenuItem("Pause sharing");
        _pause.Click += (_, _) => _ = TogglePauseAsync();
        _reconnect = new NativeMenuItem("Reconnect helper");
        _reconnect.Click += (_, _) => _ = SendCommandAsync(
            ReachHostCommand.ReconnectHelper);
        _stop = new NativeMenuItem("Stop hosting");
        _stop.Click += (_, _) => _ = SendCommandAsync(
            ReachHostCommand.StopHosting);
        var exit = new NativeMenuItem("Exit Novolis Reach Host");
        exit.Click += (_, _) => _ = ExitAfterStopAsync();
        menu.Add(open);
        menu.Add(_pause);
        menu.Add(_reconnect);
        menu.Add(_stop);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(exit);

        _tray = new TrayIcon
        {
            Icon = icon,
            ToolTipText = "Novolis Reach Host",
            IsVisible = true,
            Menu = menu,
        };
        _tray.Clicked += (_, _) => ShowWindow();
        TrayIcon.SetIcons(Application.Current, [_tray]);
        if (_lastStatus is not null)
            UpdateStatus(_lastStatus);
    }

    private async Task TogglePauseAsync()
    {
        var paused = _lastStatus?.SharingPaused ?? false;
        await SendCommandAsync(
            ReachHostCommand.SetSharingPaused,
            !paused);
    }

    private async Task SendCommandAsync(
        ReachHostCommand command,
        bool? enabled = null)
    {
        try
        {
            var response = await _client.SendAsync(
                new ReachHostControlRequest(command, enabled));
            if (response.Status is not null)
                UpdateStatus(response.Status);
        }
        catch (Exception exception)
        {
            if (_tray is not null)
                _tray.ToolTipText = $"Reach host unavailable: {exception.Message}";
        }
    }

    private async Task ExitAfterStopAsync()
    {
        await SendCommandAsync(ReachHostCommand.StopHosting);
        ExitApplication();
    }
}
