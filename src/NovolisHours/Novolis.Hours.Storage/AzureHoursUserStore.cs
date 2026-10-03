using System.Collections.Immutable;
using Novolis.Storage.Abstractions;

namespace Novolis.Hours.Storage;

/// <summary>Hours user-profile store backed by scalar Azure Table rows.</summary>
public sealed class AzureHoursUserStore(IRepository<AzureHoursUserRecord> repository) : IHoursUserStore
{
    private readonly IRepository<AzureHoursUserRecord> repository =
        repository ?? throw new ArgumentNullException(nameof(repository));

    /// <inheritdoc />
    public async ValueTask<HoursUserDocument?> FindAsync(
        Guid identityId,
        CancellationToken cancellationToken = default)
    {
        var row = await repository.TryGetAsync(identityId, cancellationToken).ConfigureAwait(false);
        return row?.ToDocument();
    }

    /// <inheritdoc />
    public HoursUserDocument? FindByEmployeeId(string employeeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        return repository.All()
            .SingleOrDefault(item =>
                string.Equals(item.EmployeeId, employeeId, StringComparison.Ordinal))
            ?.ToDocument();
    }

    /// <inheritdoc />
    public ImmutableArray<HoursUserDocument> List() =>
        repository.All()
            .Select(item => item.ToDocument())
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray();

    /// <inheritdoc />
    public ValueTask SaveAsync(
        HoursUserDocument user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        return repository.UpsertAsync(
            AzureHoursUserRecord.FromDocument(user),
            cancellationToken);
    }
}
