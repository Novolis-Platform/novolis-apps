using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Reach.Protocol;

namespace Novolis.Reach.Host.Windows;

/// <summary>Diagnostics and configuration dashboard for the host service.</summary>
public sealed class ReachHostView : UserControl
{
    private readonly ReachHostClient _client;
    private readonly ReachHostTrayController _tray;
    private readonly TextBlock _status;
    private readonly TextBlock _endpoints;
    private readonly TextBlock _clients;
    private readonly TextBlock _health;
    private readonly TextBox _log;
    private readonly Button _pause;
    private readonly Button _reconnect;
    private readonly Button _stop;
    private readonly DispatcherTimer _refreshTimer;
    private bool _sharingPaused;

    /// <summary>Creates the operator dashboard.</summary>
    public ReachHostView(
        ReachHostClient client,
        ReachHostTrayController tray)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _tray = tray ?? throw new ArgumentNullException(nameof(tray));
        GraphicalProfileBinding.Bind(
            this,
            BackgroundProperty,
            GraphicalProfile.BackgroundResourceKey);
        _status = new TextBlock { Text = "Service status: unknown" };
        _endpoints = new TextBlock { TextWrapping = TextWrapping.Wrap };
        _clients = new TextBlock { TextWrapping = TextWrapping.Wrap };
        _health = new TextBlock { TextWrapping = TextWrapping.Wrap };
        _log = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
        };
        _pause = new Button { Content = "Pause sharing" };
        _pause.Click += PauseClicked;
        _reconnect = new Button { Content = "Reconnect helper" };
        _reconnect.Click += ReconnectClicked;
        _stop = new Button { Content = "Stop hosting" };
        _stop.Click += StopClicked;
        var hideToTray = new Button { Content = "Hide to tray" };
        hideToTray.Click += (_, _) => _tray.HideToTray();
        var refresh = new Button { Content = "Refresh" };
        refresh.Click += RefreshClicked;
        _refreshTimer = new DispatcherTimer(
            TimeSpan.FromSeconds(2),
            DispatcherPriority.Background,
            async (_, _) => await RefreshAsync());

        Content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,*"),
            Margin = new global::Avalonia.Thickness(24),
            RowSpacing = 12,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { refresh, _pause, _reconnect, _stop, hideToTray },
                },
                _status,
                _endpoints,
                _clients,
                _health,
                _log,
            },
        };
        Grid.SetRow(_status, 1);
        Grid.SetRow(_endpoints, 2);
        Grid.SetRow(_clients, 3);
        Grid.SetRow(_health, 4);
        Grid.SetRow(_log, 5);
        AttachedToVisualTree += async (_, _) =>
        {
            _refreshTimer.Start();
            await RefreshAsync();
        };
        DetachedFromVisualTree += (_, _) => _refreshTimer.Stop();
    }

    private async void RefreshClicked(
        object? sender,
        global::Avalonia.Interactivity.RoutedEventArgs args) =>
        await RefreshAsync();

    private async void PauseClicked(
        object? sender,
        global::Avalonia.Interactivity.RoutedEventArgs args)
    {
        try
        {
            var response = await _client.SendAsync(
                new ReachHostControlRequest(
                    ReachHostCommand.SetSharingPaused,
                    !_sharingPaused));
            Apply(response);
        }
        catch (Exception exception)
        {
            _status.Text = $"Service unavailable: {exception.Message}";
        }
    }

    private async void ReconnectClicked(
        object? sender,
        global::Avalonia.Interactivity.RoutedEventArgs args)
    {
        await SendCommandAsync(ReachHostCommand.ReconnectHelper);
    }

    private async void StopClicked(
        object? sender,
        global::Avalonia.Interactivity.RoutedEventArgs args)
    {
        await SendCommandAsync(ReachHostCommand.StopHosting);
    }

    private async Task RefreshAsync()
    {
        try
        {
            var response = await _client.SendAsync(
                new ReachHostControlRequest(ReachHostCommand.GetStatus));
            Apply(response);
        }
        catch (Exception exception)
        {
            _status.Text = $"Service unavailable: {exception.Message}";
        }
    }

    private async Task SendCommandAsync(ReachHostCommand command)
    {
        try
        {
            Apply(await _client.SendAsync(
                new ReachHostControlRequest(command)));
        }
        catch (Exception exception)
        {
            _status.Text = $"Service unavailable: {exception.Message}";
        }
    }

    private void Apply(ReachHostControlResponse response)
    {
        var status = response.Status;
        if (status is null)
        {
            _status.Text = response.Message;
            return;
        }

        _sharingPaused = status.SharingPaused;
        _pause.Content = _sharingPaused ? "Resume sharing" : "Pause sharing";
        _status.Text = $"Service: {status.State}; user: {status.InteractiveUser ?? "none"}";
        _endpoints.Text = $"Tailscale endpoints: "
            + (status.Endpoints.Length == 0
                ? "none"
                : string.Join(", ", status.Endpoints));
        _clients.Text = $"Connected clients: {status.ConnectedClients}";
        var performance = status.Performance;
        _health.Text = performance is null
            ? "Health: no performance data yet."
            : $"Health: received {performance.ReceivedFrames} frames, "
              + $"sent {performance.SentFrames}, dropped {performance.DroppedFrames}; "
              + $"frame age p95: "
              + $"{FormatMilliseconds(performance.FrameAgeP95Milliseconds)}; "
              + $"input RTT p95: "
              + $"{FormatMilliseconds(performance.InputRoundTripP95Milliseconds)}.";
        _log.Text = string.Join(Environment.NewLine, status.RecentMessages);
        _tray.UpdateStatus(status);
    }

    private static string FormatMilliseconds(double? milliseconds) =>
        milliseconds is { } value ? $"{value:0} ms" : "n/a";
}
