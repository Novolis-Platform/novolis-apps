using System.Collections.Immutable;

namespace Novolis.Hours.Domain;

/// <summary>Append-only boundary used by the domain and replaceable by JSON or in-memory implementations.</summary>
public interface IHoursJournal
{
    /// <summary>Writes an event exactly once.</summary>
    ValueTask AppendAsync(HoursEvent entry, CancellationToken cancellationToken = default);

    /// <summary>Reads all events for one employee in journal order.</summary>
    ValueTask<ImmutableArray<HoursEvent>> ReadEmployeeAsync(
        string employeeId,
        CancellationToken cancellationToken = default);

    /// <summary>Reads all journal events for cross-employee reviewer lookup and report generation.</summary>
    ValueTask<ImmutableArray<HoursEvent>> ReadAllAsync(
        CancellationToken cancellationToken = default);
}
