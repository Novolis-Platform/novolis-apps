using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Novolis.Reach.Protocol;
using Novolis.Transports.Discovery;
using Novolis.Transports.Tailscale;

namespace Novolis.Reach.Client;

/// <summary>Finds Reach hosts on LAN broadcast and Tailscale interfaces.</summary>
public static class ReachClientDiscovery
{
    /// <summary>
    /// Sends the product-neutral discovery probe to local broadcast targets and
    /// returns compatible Reach endpoints found during the scan.
    /// </summary>
    public static async Task<IReadOnlyList<ReachDiscoveredHost>> ScanAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return await ScanAsync(
                timeout,
                ReachClientDiscoveryOptions.FromEnvironment(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Scans LAN broadcast plus any explicitly configured HTTP(S) registry and
    /// DNS SRV/TXT sources.
    /// </summary>
    public static async Task<IReadOnlyList<ReachDiscoveredHost>> ScanAsync(
        TimeSpan timeout,
        ReachClientDiscoveryOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(options);

        var targets = GetProbeTargets();
        var scans = targets.Select(target =>
            ScanTargetAsync(target, timeout, cancellationToken));
        var legacyScan = Task.WhenAll(scans);
        var routedScan = ScanRoutedSourcesAsync(
            options,
            timeout,
            cancellationToken);
        var legacyResults = await legacyScan.ConfigureAwait(false);
        var routedResults = await routedScan.ConfigureAwait(false);

        return legacyResults
            .SelectMany(static result => result)
            .Concat(routedResults)
            .DistinctBy(static host => host.Endpoint, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static host => host.HostName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static host => host.Endpoint, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task<IReadOnlyList<ReachDiscoveredHost>>
        ScanRoutedSourcesAsync(
            ReachClientDiscoveryOptions options,
            TimeSpan timeout,
            CancellationToken cancellationToken)
    {
        var query = new ServiceDiscoveryQuery(
            options.Cohort,
            options.ServiceName);
        var sourceTasks = new List<Task<IReadOnlyList<ServiceRegistration>>>();
        foreach (var address in options.HttpRegistryAddresses)
        {
            if (address is null)
                continue;

            sourceTasks.Add(
                ReadSourceSafelyAsync(
                    () => ScanHttpRegistryAsync(
                        address,
                        query,
                        timeout,
                        cancellationToken),
                    cancellationToken));
        }

        if (!string.IsNullOrWhiteSpace(options.DnsDomain))
        {
            sourceTasks.Add(
                ReadSourceSafelyAsync(
                    () => ScanDnsAsync(
                        options,
                        query,
                        cancellationToken),
                    cancellationToken));
        }

        if (sourceTasks.Count == 0)
            return Array.Empty<ReachDiscoveredHost>();

        var sourceResults = await Task.WhenAll(sourceTasks)
            .ConfigureAwait(false);
        var map = new ServiceDiscoveryMap();
        map.Replace(sourceResults.SelectMany(static result => result));

        return map.Find(query)
            .SelectMany(ToReachHosts)
            .ToArray();
    }

    private static async Task<IReadOnlyList<ServiceRegistration>>
        ReadSourceSafelyAsync(
            Func<Task<IReadOnlyList<ServiceRegistration>>> read,
            CancellationToken cancellationToken)
    {
        try
        {
            return await read().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Optional routed sources must not hide a reachable LAN host.
            return Array.Empty<ServiceRegistration>();
        }
    }

    private static async Task<IReadOnlyList<ServiceRegistration>>
        ScanHttpRegistryAsync(
            Uri address,
            ServiceDiscoveryQuery query,
            TimeSpan timeout,
            CancellationToken cancellationToken)
    {
        using var http = new HttpClient
        {
            Timeout = timeout,
        };
        var registry = new HttpServiceRegistry(http, address);
        return await registry.DiscoverAsync(query, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<ServiceRegistration>>
        ScanDnsAsync(
            ReachClientDiscoveryOptions options,
            ServiceDiscoveryQuery query,
            CancellationToken cancellationToken)
    {
        var source = new DnsSrvServiceDiscovery(
            new DnsSrvServiceDiscoveryOptions(options.DnsDomain!)
            {
                Protocol = options.DnsProtocol,
                EndpointScheme = options.DnsEndpointScheme,
            });
        return await source.DiscoverAsync(query, cancellationToken)
            .ConfigureAwait(false);
    }

    private static IEnumerable<ReachDiscoveredHost> ToReachHosts(
        ServiceRegistration registration)
    {
        var hostName = registration.Metadata.TryGetValue(
                "hostName",
                out var advertisedHostName)
            && !string.IsNullOrWhiteSpace(advertisedHostName)
            ? advertisedHostName
            : registration.InstanceId;
        var protocolVersion = registration.Metadata.TryGetValue(
                "protocolVersion",
                out var advertisedProtocolVersion)
            && !string.IsNullOrWhiteSpace(advertisedProtocolVersion)
            ? advertisedProtocolVersion
            : ReachProtocol.Version;

        foreach (var endpoint in registration.Endpoints)
        {
            if (TryNormalizeEndpoint(
                    endpoint.Address.ToString(),
                    out var normalized))
            {
                yield return new ReachDiscoveredHost(
                    hostName,
                    protocolVersion,
                    normalized);
            }
        }
    }

    private static async Task<IReadOnlyList<ReachDiscoveredHost>> ScanTargetAsync(
        IPEndPoint target,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var hosts = new List<ReachDiscoveredHost>();
        try
        {
            var scanner = new DiscoveryScanner();
            await foreach (var beacon in scanner.ScanAsync(
                               target,
                               ReachProtocol.DiscoveryProbe,
                               timeout,
                               cancellationToken))
            {
                if (!string.Equals(
                        beacon.ApplicationId,
                        ReachProtocol.AppId,
                        StringComparison.Ordinal)
                    || !ReachProtocol.IsCompatible(beacon.ProtocolVersion))
                {
                    continue;
                }

                foreach (var endpoint in beacon.Endpoints)
                {
                    if (TryNormalizeEndpoint(endpoint, out var normalized))
                    {
                        hosts.Add(new ReachDiscoveredHost(
                            beacon.HostName,
                            beacon.ProtocolVersion,
                            normalized));
                    }
                }
            }
        }
        catch (SocketException)
        {
            // A disabled or isolated adapter must not prevent other scans.
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }

        return hosts;
    }

    private static IReadOnlyList<IPEndPoint> GetProbeTargets()
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var endpoints = new List<IPEndPoint>
        {
            new(IPAddress.Loopback, ReachProtocol.DiscoveryPort),
            new(IPAddress.Broadcast, ReachProtocol.DiscoveryPort),
        };

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
                continue;

            foreach (var unicast in networkInterface
                         .GetIPProperties()
                         .UnicastAddresses
                         .Where(static item =>
                             item.Address.AddressFamily == AddressFamily.InterNetwork
                             && IsPrivateOrTailscaleIPv4(item.Address)))
            {
                var address = unicast.Address;
                AddTarget(endpoints, targets, address);

                if (unicast.IPv4Mask is not null)
                {
                    var broadcast = GetBroadcastAddress(address, unicast.IPv4Mask);
                    AddTarget(endpoints, targets, broadcast);
                }
            }
        }

        return endpoints;
    }

    private static void AddTarget(
        ICollection<IPEndPoint> endpoints,
        ISet<string> targets,
        IPAddress address)
    {
        var key = address + ":" + ReachProtocol.DiscoveryPort;
        if (targets.Add(key))
            endpoints.Add(new IPEndPoint(address, ReachProtocol.DiscoveryPort));
    }

    private static IPAddress GetBroadcastAddress(
        IPAddress address,
        IPAddress mask)
    {
        var addressBytes = address.GetAddressBytes();
        var maskBytes = mask.GetAddressBytes();
        var broadcastBytes = new byte[addressBytes.Length];
        for (var index = 0; index < broadcastBytes.Length; index++)
            broadcastBytes[index] = (byte)(addressBytes[index] | ~maskBytes[index]);

        return new IPAddress(broadcastBytes);
    }

    private static bool TryNormalizeEndpoint(
        string endpoint,
        out string normalized)
    {
        normalized = string.Empty;
        if (!Uri.TryCreate(
                endpoint.Contains("://", StringComparison.Ordinal)
                    ? endpoint
                    : $"tcp://{endpoint}",
                UriKind.Absolute,
                out var uri)
            || uri.Port <= 0)
        {
            return false;
        }

        var scheme = uri.Scheme.ToLowerInvariant();
        if (scheme is not ("tcp" or "quic"))
            return false;

        var host = uri.Host;
        if (OperatingSystem.IsAndroid()
            && (IPAddress.TryParse(host, out var address)
                && IPAddress.IsLoopback(address)
                || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)))
        {
            // The Android emulator reaches the Windows host through this
            // gateway address; a host's loopback beacon is otherwise resolved
            // inside the Android guest.
            host = "10.0.2.2";
        }

        normalized = $"{scheme}://{host}:{uri.Port}{uri.Query}";
        return true;
    }

    private static bool IsPrivateOrTailscaleIPv4(IPAddress address)
    {
        if (TailscaleAddressEnumerator.IsTailscaleIPv4(address))
            return true;

        var bytes = address.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168)
            || (bytes[0] == 169 && bytes[1] == 254);
    }
}
