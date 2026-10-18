using Novolis.Reach.Protocol;

namespace Novolis.Reach.Client;

/// <summary>Optional routed discovery sources for Reach clients.</summary>
public sealed record ReachClientDiscoveryOptions
{
    /// <summary>
    /// Reads optional routed discovery settings from the process environment.
    /// </summary>
    public static ReachClientDiscoveryOptions FromEnvironment()
    {
        var registryText =
            Environment.GetEnvironmentVariable(
                "NOVOLIS_REACH_DISCOVERY_REGISTRIES")
            ?? Environment.GetEnvironmentVariable(
                "NOVOLIS_REACH_DISCOVERY_REGISTRY");
        var registries = (registryText ?? string.Empty)
            .Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries)
            .Select(static value =>
                Uri.TryCreate(value, UriKind.Absolute, out var uri)
                    && (uri.Scheme is "http" or "https")
                    ? uri
                    : null)
            .Where(static uri => uri is not null)
            .Cast<Uri>()
            .ToArray();

        return new ReachClientDiscoveryOptions
        {
            HttpRegistryAddresses = registries,
            DnsDomain = Environment.GetEnvironmentVariable(
                "NOVOLIS_REACH_DISCOVERY_DNS_DOMAIN"),
        };
    }

    /// <summary>Gets the cohort queried by routed sources.</summary>
    public string Cohort { get; init; } = ReachProtocol.DiscoveryCohort;

    /// <summary>Gets the service name queried by routed sources.</summary>
    public string ServiceName { get; init; } =
        ReachProtocol.DiscoveryServiceName;

    /// <summary>
    /// Gets HTTP(S) registry addresses. Each address may include a path prefix.
    /// </summary>
    public IReadOnlyList<Uri> HttpRegistryAddresses { get; init; } =
        Array.Empty<Uri>();

    /// <summary>
    /// Gets the DNS suffix used for SRV/TXT discovery, or <see langword="null"/>.
    /// </summary>
    public string? DnsDomain { get; init; }

    /// <summary>Gets the DNS SRV protocol label.</summary>
    public string DnsProtocol { get; init; } = "tcp";

    /// <summary>Gets the URI scheme of DNS-resolved endpoints.</summary>
    public string DnsEndpointScheme { get; init; } = "tcp";
}
