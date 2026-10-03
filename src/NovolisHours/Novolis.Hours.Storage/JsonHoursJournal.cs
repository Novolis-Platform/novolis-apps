using System.Collections.Immutable;
using Novolis.Hours.Domain;
using Novolis.Storage.Abstractions;

namespace Novolis.Hours.Storage;

/// <summary>Append-only journal backed by the Novolis JSON repository provider.</summary>
public sealed class JsonHoursJournal : IHoursJournal
{
    private readonly IRepository<HoursJournalDocument> repository;
    private readonly HoursChangeFeed changeFeed;

    /// <summary>Initializes a JSON-backed journal.</summary>
    public JsonHoursJournal(
        IRepository<HoursJournalDocument> repository,
        HoursChangeFeed changeFeed)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.changeFeed = changeFeed ?? throw new ArgumentNullException(nameof(changeFeed));
    }

    /// <inheritdoc />
    public async ValueTask AppendAsync(HoursEvent entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var existing = await repository.TryGetAsync(entry.Id, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Journal event '{entry.Id}' already exists.");
        }

        await repository.UpsertAsync(new HoursJournalDocument(entry.Id, entry), cancellationToken);
        changeFeed.Publish(entry);
    }

    /// <inheritdoc />
    public ValueTask<ImmutableArray<HoursEvent>> ReadEmployeeAsync(
        string employeeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(repository.All()
            .Select(item => item.Event)
            .Where(item => string.Equals(item.EmployeeId, employeeId, StringComparison.Ordinal))
            .OrderBy(item => item.OccurredAtUtc)
            .ThenBy(item => item.Id)
            .ToImmutableArray());
    }

    /// <inheritdoc />
    public ValueTask<ImmutableArray<HoursEvent>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(repository.All()
            .Select(item => item.Event)
            .OrderBy(item => item.OccurredAtUtc)
            .ThenBy(item => item.Id)
            .ToImmutableArray());
    }
}
