using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novolis.Transports;
using Novolis.Transports.Discovery;
using Novolis.Transports.Quic;
using Novolis.Transports.Udp;
using Novolis.Windows.Sessions;

namespace Novolis.Reach.Host.Server;

/// <summary>
/// Headless Reach host service. It accepts clients on Tailscale and bridges
/// session commands to the interactive-session helper over local IPC.
/// </summary>
public sealed class ReachHostService : BackgroundService
{
    private readonly ReachHostRuntime _runtime;

    /// <summary>Creates the host service.</summary>
    public ReachHostService(
        ILogger<ReachHostService> log,
        WindowsSessionManager sessions)
    {
        _runtime = new ReachHostRuntime(log, sessions);
    }

    /// <summary>Gets the current operator status.</summary>
    public ReachHostStatus GetStatus() => _runtime.CreateStatus();

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken,
            _runtime.Lifetime.Token);
        var cancellationToken = linked.Token;
        _runtime.WriteLog("Reach host service starting.");

        var addresses = new[] { IPAddress.Loopback }
            .Concat(ReachHostCertificates.GetReachableIPv4Addresses())
            .Distinct()
            .ToArray();
        X509Certificate2? quicCertificate = null;
        if (QuicTransportListener.IsSupported)
        {
            try
            {
                quicCertificate = ReachHostCertificates.CreateQuicCertificate();
            }
            catch (Exception exception)
            {
                _runtime.WriteLog(
                    $"QUIC certificate initialization failed: {exception.Message}");
            }
        }

        var quicPin = quicCertificate?.GetCertHashString(HashAlgorithmName.SHA256);
        _runtime.Endpoints = addresses
            .SelectMany(address => quicPin is { Length: > 0 }
                ? new[]
                {
                    $"quic://{address}:{ReachProtocol.ControlPort}?pin={quicPin}",
                    $"tcp://{address}:{ReachProtocol.ControlPort}",
                }
                : new[]
                {
                    $"tcp://{address}:{ReachProtocol.ControlPort}",
                })
            .ToArray();
        if (addresses.Length == 0)
            _runtime.WriteLog(
                "No private IPv4 adapter is available; remote listening is paused.");

        ITransportDatagramChannel? datagramChannel = null;
        try
        {
            datagramChannel = new UdpDatagramChannel(
                new IPEndPoint(IPAddress.Any, ReachProtocol.MediaPort));
            _runtime.DatagramChannel = datagramChannel;
            _runtime.WriteLog(
                $"Listening for authenticated Reach UDP media on "
                + $"{datagramChannel.LocalEndPoint}.");
        }
        catch (SocketException exception)
        {
            _runtime.WriteLog(
                $"Could not listen for Reach UDP media on port "
                + $"{ReachProtocol.MediaPort}: {exception.Message}");
        }

        var tasks = new List<Task>
        {
            _runtime.Operator.RunAsync(cancellationToken),
            _runtime.Session.RunAsync(cancellationToken),
            RunDiscoveryAsync(cancellationToken),
        };
        if (datagramChannel is not null)
        {
            tasks.Add(
                _runtime.Media.RunDatagramAsync(datagramChannel, cancellationToken));
        }

        foreach (var address in addresses)
        {
            tasks.Add(_runtime.Control.RunTcpAsync(address, cancellationToken));
            tasks.Add(_runtime.Media.RunTcpAsync(address, cancellationToken));
            if (quicCertificate is not null)
            {
                tasks.Add(
                    _runtime.Control.RunQuicAsync(
                        address,
                        quicCertificate,
                        cancellationToken));
            }
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            foreach (var listener in _runtime.Listeners)
                await listener.DisposeAsync().ConfigureAwait(false);
            foreach (var listener in _runtime.MediaListeners)
                await listener.DisposeAsync().ConfigureAwait(false);
            if (datagramChannel is not null)
                await datagramChannel.DisposeAsync().ConfigureAwait(false);
            _runtime.DatagramChannel = null;
            foreach (var client in _runtime.Clients.Values)
                await client.DisposeAsync().ConfigureAwait(false);
            quicCertificate?.Dispose();
            var sessionConnection = Interlocked.Exchange(
                ref _runtime.SessionConnection,
                null);
            if (sessionConnection is not null)
                await sessionConnection.DisposeAsync().ConfigureAwait(false);
            _runtime.WriteLog("Reach host service stopped.");
        }
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _runtime.Lifetime.Cancel();
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RunDiscoveryAsync(CancellationToken cancellationToken)
    {
        await using var responder = new DiscoveryResponder(
            new IPEndPoint(IPAddress.Any, ReachProtocol.DiscoveryPort),
            ReachProtocol.DiscoveryProbe,
            new DiscoveryBeacon(
                ReachProtocol.AppId,
                ReachProtocol.Version,
                Environment.MachineName,
                _runtime.Endpoints));
        await responder.RunAsync(cancellationToken).ConfigureAwait(false);
    }
}
