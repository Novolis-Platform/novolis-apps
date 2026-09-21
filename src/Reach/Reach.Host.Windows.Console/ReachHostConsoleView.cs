using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Novolis.Reach.Protocol;

namespace Novolis.Reach.Host.Windows.Console;

/// <summary>Diagnostics and configuration dashboard for the host service.</summary>
public sealed class ReachHostConsoleView : UserControl
{
    private readonly ReachHostConsoleClient _client;
    private readonly TextBlock _status;
    private readonly TextBlock _endpoints;
    private readonly TextBlock _clients;
    private readonly TextBox _log;
    private readonly Button _pause;
    private bool _sharingPaused;

    /// <summary>Creates the operator dashboard.</summary>
    public ReachHostConsoleView(ReachHostConsoleClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _status = new TextBlock { Text = "Service status: unknown" };
        _endpoints = new TextBlock { TextWrapping = TextWrapping.Wrap };
        _clients = new TextBlock { TextWrapping = TextWrapping.Wrap };
        _log = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
        };
        _pause = new Button { Content = "Pause sharing" };
        _pause.Click += PauseClicked;
        var refresh = new Button { Content = "Refresh" };
        refresh.Click += RefreshClicked;

        Content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*"),
            Margin = new global::Avalonia.Thickness(24),
            RowSpacing = 12,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { refresh, _pause },
                },
                _status,
                _endpoints,
                _clients,
                _log,
            },
        };
        Grid.SetRow(_status, 1);
        Grid.SetRow(_endpoints, 2);
        Grid.SetRow(_clients, 3);
        Grid.SetRow(_log, 4);
        AttachedToVisualTree += async (_, _) => await RefreshAsync();
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
        _log.Text = string.Join(Environment.NewLine, status.RecentMessages);
    }
}
