using System.Collections.Immutable;
using Novolis.Storage.Abstractions;

namespace Novolis.Hours.Storage;

/// <summary>Adapts the normal repository provider to the Hours user-profile boundary.</summary>
public sealed class RepositoryHoursUserStore(IRepository<HoursUserDocument> repository) : IHoursUserStore
{
    private readonly IRepository<HoursUserDocument> repository =
        repository ?? throw new ArgumentNullException(nameof(repository));

    /// <inheritdoc />
    public ValueTask<HoursUserDocument?> FindAsync(
        Guid identityId,
        CancellationToken cancellationToken = default) =>
        repository.TryGetAsync(identityId, cancellationToken);

    /// <inheritdoc />
    public HoursUserDocument? FindByEmployeeId(string employeeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        return repository.All().SingleOrDefault(item =>
            string.Equals(item.EmployeeId, employeeId, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public ImmutableArray<HoursUserDocument> List() =>
        repository.All()
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray();

    /// <inheritdoc />
    public ValueTask SaveAsync(
        HoursUserDocument user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        return repository.UpsertAsync(user, cancellationToken);
    }
}
