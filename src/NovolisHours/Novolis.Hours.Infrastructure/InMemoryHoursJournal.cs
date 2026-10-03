using System.Collections.Immutable;
using Novolis.Hours.Domain;

namespace Novolis.Hours.Infrastructure;

/// <summary>Thread-safe in-memory append-only journal used by full-application feature tests.</summary>
public sealed class InMemoryHoursJournal : IHoursJournal
{
    private readonly object gate = new();
    private ImmutableArray<HoursEvent> entries = [];
    private readonly HoursChangeFeed? changeFeed;

    /// <summary>Initializes the journal and optionally emits every durable append into the shared change feed.</summary>
    public InMemoryHoursJournal(HoursChangeFeed? changeFeed = null)
    {
        this.changeFeed = changeFeed;
    }

    /// <inheritdoc />
    public ValueTask AppendAsync(HoursEvent entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (entries.Any(item => item.Id == entry.Id))
            {
                throw new InvalidOperationException($"Journal event '{entry.Id}' already exists.");
            }

            entries = entries.Add(entry);
        }

        changeFeed?.Publish(entry);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<ImmutableArray<HoursEvent>> ReadEmployeeAsync(
        string employeeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return ValueTask.FromResult(entries
                .Where(item => string.Equals(item.EmployeeId, employeeId, StringComparison.Ordinal))
                .ToImmutableArray());
        }
    }

    /// <inheritdoc />
    public ValueTask<ImmutableArray<HoursEvent>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return ValueTask.FromResult(entries);
        }
    }
}
