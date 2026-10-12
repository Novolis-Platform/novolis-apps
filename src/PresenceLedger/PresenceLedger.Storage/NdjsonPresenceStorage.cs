using System.Runtime.CompilerServices;
using PresenceLedger.Core;

namespace PresenceLedger.Storage;

/// <summary>Convenience bundle of the local ledger stores.</summary>
public sealed class NdjsonPresenceStorage
{
    /// <summary>Creates stores beneath an application-private root.</summary>
    public NdjsonPresenceStorage(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        Directory.CreateDirectory(rootDirectory);
        RootDirectory = rootDirectory;
        Locations = new NdjsonTrackedLocationStore(
            System.IO.Path.Combine(rootDirectory, "locations.ndjson"));
        Events = new NdjsonPresenceEventStore(
            System.IO.Path.Combine(rootDirectory, "presence.ndjson"));
        States = new NdjsonPresenceStateStore(
            System.IO.Path.Combine(rootDirectory, "states.ndjson"));
        Observations = new NdjsonPresenceObservationStore(rootDirectory);
        Diagnostics = new NdjsonPresenceDiagnosticsStore(
            System.IO.Path.Combine(rootDirectory, "diagnostics.ndjson"));
    }

    /// <summary>Private directory that holds the ledger files.</summary>
    public string RootDirectory { get; }

    /// <summary>Location definition store.</summary>
    public NdjsonTrackedLocationStore Locations { get; }

    /// <summary>Semantic event store.</summary>
    public NdjsonPresenceEventStore Events { get; }

    /// <summary>Operational inference state store.</summary>
    public NdjsonPresenceStateStore States { get; }

    /// <summary>Versioned daily raw observation store.</summary>
    public NdjsonPresenceObservationStore Observations { get; }

    /// <summary>Operational diagnostics store kept separate from observations.</summary>
    public NdjsonPresenceDiagnosticsStore Diagnostics { get; }

    /// <summary>Path of the generated all-observations export.</summary>
    public string CumulativeFilePath =>
        System.IO.Path.Combine(RootDirectory, "cumulative.ndjson");

    /// <summary>
    /// Rebuilds the cumulative observation export in timestamp order.
    /// </summary>
    public async Task<int> BuildCumulativeAsync(
        CancellationToken cancellationToken = default)
    {
        var observations = new List<PresenceObservationRecord>();
        await foreach (var observation in Observations.ReadAllAsync(cancellationToken))
            observations.Add(observation);

        observations.Sort(static (left, right) =>
        {
            var result = left.At.CompareTo(right.At);
            return result != 0
                ? result
                : left.SchemaVersion.CompareTo(right.SchemaVersion);
        });

        await new NdjsonFile(CumulativeFilePath)
            .ReplaceAsync(observations, cancellationToken);
        return observations.Count;
    }
}
