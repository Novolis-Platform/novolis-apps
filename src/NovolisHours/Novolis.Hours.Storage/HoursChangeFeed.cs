using System.Threading.Channels;
using Novolis.Hours.Domain;

namespace Novolis.Hours.Storage;

/// <summary>In-process channel for durable journal changes that need realtime projections.</summary>
public sealed class HoursChangeFeed
{
    private readonly Channel<HoursEvent> channel = Channel.CreateUnbounded<HoursEvent>(
        new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = true,
            AllowSynchronousContinuations = false,
        });

    /// <summary>Gets the durable-change reader for a single projection worker.</summary>
    public ChannelReader<HoursEvent> Reader => channel.Reader;

    /// <summary>Publishes an event after it was successfully appended to the configured journal.</summary>
    public void Publish(HoursEvent entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!channel.Writer.TryWrite(entry))
        {
            throw new InvalidOperationException("The Hours change feed is not available.");
        }
    }
}
