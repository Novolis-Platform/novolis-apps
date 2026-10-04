using Novolis.Transports.Datagrams;

namespace Novolis.Reach.Transport;

/// <summary>Reassembles bounded Reach media fragments.</summary>
public sealed class ReachDatagramReassembler
{
    private readonly DatagramReassembler _inner;

    /// <summary>Creates a reassembler with bounded in-flight state.</summary>
    public ReachDatagramReassembler(
        int maximumFrames = 8,
        TimeSpan? maximumAge = null)
    {
        _inner = new DatagramReassembler(maximumFrames, maximumAge);
    }

    /// <summary>Gets the number of incomplete sequences currently retained.</summary>
    public int PendingCount => _inner.PendingCount;

    /// <summary>
    /// Adds one fragment and returns a complete payload only when all fragments
    /// have arrived before their deadline.
    /// </summary>
    public bool TryAccept(
        ReachDatagramFragment fragment,
        DateTimeOffset now,
        out byte[] payload) =>
        _inner.TryAccept(
            new DatagramFragment(
                fragment.Sequence,
                fragment.FragmentIndex,
                fragment.FragmentCount,
                fragment.Payload),
            now,
            out payload);
}
