using System.Collections.Immutable;
using Novolis.Hours.Domain;
using Novolis.Storage.Abstractions;

namespace Novolis.Hours.Storage;

/// <summary>Append-only Hours journal backed by Azure Table Storage or Azurite.</summary>
public sealed class AzureHoursJournal(
    IRepository<AzureHoursJournalDocument> repository,
    HoursChangeFeed changeFeed) : IHoursJournal
{
    private readonly IRepository<AzureHoursJournalDocument> repository =
        repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly HoursChangeFeed changeFeed =
        changeFeed ?? throw new ArgumentNullException(nameof(changeFeed));

    /// <inheritdoc />
    public async ValueTask AppendAsync(
        HoursEvent entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var existing = await repository.TryGetAsync(entry.Id, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Journal event '{entry.Id}' already exists.");
        }

        await repository.UpsertAsync(
            AzureHoursJournalDocument.FromEvent(entry),
            cancellationToken).ConfigureAwait(false);
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
            .Where(item => string.Equals(item.EmployeeId, employeeId, StringComparison.Ordinal))
            .Select(item => item.ToEvent())
            .OrderBy(item => item.OccurredAtUtc)
            .ThenBy(item => item.Id)
            .ToImmutableArray());
    }

    /// <inheritdoc />
    public ValueTask<ImmutableArray<HoursEvent>> ReadAllAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(repository.All()
            .Select(item => item.ToEvent())
            .OrderBy(item => item.OccurredAtUtc)
            .ThenBy(item => item.Id)
            .ToImmutableArray());
    }
}
