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
}
