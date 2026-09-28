using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Novolis.Reach.Host.Windows;

/// <summary>Owns the per-user Reach host notification-area companion.</summary>
public sealed class ReachHostTrayController : IDisposable
{
    private Window? _window;
    private TrayIcon? _tray;
    private bool _forceExit;

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
        var exit = new NativeMenuItem("Exit Novolis Reach Host");
        exit.Click += (_, _) => ExitApplication();
        menu.Add(open);
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
    }
}
