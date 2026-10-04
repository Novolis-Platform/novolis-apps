using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Novolis.Avalonia.Video;
using Novolis.Reach.Client;
using Novolis.Video;

namespace Novolis.Avalonia.Reach;

internal sealed class ReachClientViewConnection(ReachClientView view)
{
        internal void EndpointTextChanged(object? sender, TextChangedEventArgs args)
        {
            Volatile.Write(
                ref view._endpointValue,
                view._endpoint.Text ?? string.Empty);
            view.UpdateConnectionControls();
        }

        internal void DiscoveredHostSelected(
            object? sender,
            SelectionChangedEventArgs args)
        {
            if (view._discoveredHosts.SelectedItem is not string label
                || !view._discoveredHostEndpoints.TryGetValue(label, out var endpoint))
            {
                return;
            }

            view._endpoint.Text = endpoint;
            view.OnStatusChanged($"Selected {label}.");
            view.UpdateConnectionControls();
        }

        internal async void DiscoverClicked(
            object? sender,
            global::Avalonia.Interactivity.RoutedEventArgs args)
        {
            await view.DiscoverHostsAsync();
        }

        internal async Task DiscoverHostsAsync()
        {
            view._discoveryActive = true;
            view._discover.IsEnabled = false;
            view._connect.IsEnabled = false;
            view._status.Text = "Searching for Reach hosts on LAN and Tailscale...";
            try
            {
                var hosts = await ReachClientDiscovery.ScanAsync(
                        TimeSpan.FromSeconds(2));
                if (hosts.Count == 0)
                {
                    view._status.Text = "No Reach hosts found on LAN or Tailscale.";
                    ApplyRememberedHostList();
                    return;
                }

                view._discoveredHostEndpoints.Clear();
                var labels = new List<string>();
                foreach (var host in hosts)
                {
                    var label = $"{host.HostName} — {host.Endpoint}";
                    view._discoveredHostEndpoints[label] = host.Endpoint;
                    labels.Add(label);
                }

                view._discoveredHosts.ItemsSource = labels;
                view._discoveredHosts.IsVisible = labels.Count > 0;
                foreach (var host in hosts)
                {
                    view._endpoint.Text = host.Endpoint;
                    if (await view.ConnectToEndpointAsync())
                        return;
                }

                view._status.Text = $"Found {hosts.Count} Reach hosts, but none accepted a connection.";
            }
            catch (Exception exception)
            {
                view._status.Text = $"Discovery failed: {exception.Message}";
            }
            finally
            {
                view._discoveryActive = false;
                view._discover.IsEnabled = true;
                view.UpdateConnectionControls();
            }
        }

        internal void ApplyRememberedHostList()
        {
            var remembered = ReachClientEndpointStore.Load().ToArray();
            view._discoveredHostEndpoints.Clear();
            var labels = new List<string>();
            foreach (var endpoint in remembered)
            {
                var label = $"Remembered — {endpoint}";
                view._discoveredHostEndpoints[label] = endpoint;
                labels.Add(label);
            }

            view._discoveredHosts.ItemsSource = labels;
            view._discoveredHosts.IsVisible = labels.Count > 0;
        }

        internal async void ConnectClicked(
            object? sender,
            global::Avalonia.Interactivity.RoutedEventArgs args)
        {
            if (view._session.State == ReachClientConnectionState.Connecting)
            {
                view._connectCancellation?.Cancel();
                return;
            }

            if (view._session.IsConnected || view._session.State == ReachClientConnectionState.Lost)
            {
                await view.ReconnectToEndpointAsync();
                return;
            }

            await view.ConnectToEndpointAsync();
        }

        internal async Task<bool> ConnectToEndpointAsync()
        {
            view.CancelReconnect();
            view._sessionEnded = false;
            view._connect.IsEnabled = false;
            view._platformVideoConfigured = false;
            view._streamStatusShown = false;
            view.ResetStatusPriority();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            view._connectCancellation = timeout;
            try
            {
                await view._session.ConnectAsync(
                    view._endpoint.Text ?? string.Empty,
                    ReachClientView.ResolvePlatform(),
                    Environment.MachineName,
                    timeout.Token);
                var capabilities = view._session.NegotiatedCapabilities;
                view._capabilities.Text = capabilities is null
                    ? "Capabilities: none"
                    : $"Capabilities: {capabilities.Features}; "
                      + $"video={string.Join(",", capabilities.OfferedVideoCodecs)}";
                ReachClientEndpointStore.Remember(view._endpoint.Text);
                view._discoveredHosts.IsVisible = false;
                view.SetConnectedStatus();
                return true;
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                view.OnStatusChanged("Connection cancelled.");
                return false;
            }
            catch (Exception exception)
            {
                view.OnStatusChanged($"Connection failed: {exception.Message}");
                return false;
            }
            finally
            {
                _ = Interlocked.CompareExchange(
                    ref view._connectCancellation,
                    null,
                    timeout);
                view.UpdateConnectionControls();
            }
        }

        internal async Task<bool> ReconnectToEndpointAsync(
            CancellationToken cancellationToken = default,
            bool cancelExistingReconnect = true)
        {
            if (cancelExistingReconnect)
                view.CancelReconnect();
            view._connect.IsEnabled = false;
            view._sessionEnded = false;
            view._platformVideoConfigured = false;
            view._streamStatusShown = false;
            view.ResetStatusPriority();
            view.ClearVideoFrame();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                await view._session.ReconnectAsync(timeout.Token);
                view._capabilities.Text = view._session.NegotiatedCapabilities is { } capabilities
                    ? $"Capabilities: {capabilities.Features}; "
                      + $"video={string.Join(",", capabilities.OfferedVideoCodecs)}"
                    : "Capabilities: none";
                view.SetConnectedStatus(reconnected: true);
                return true;
            }
            catch (Exception exception)
            {
                view.OnStatusChanged($"Reconnect failed: {exception.Message}");
                return false;
            }
            finally
            {
                view.UpdateConnectionControls();
            }
        }

}

