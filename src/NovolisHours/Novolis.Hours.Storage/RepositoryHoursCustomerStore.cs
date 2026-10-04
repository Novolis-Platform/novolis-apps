using System.Collections.Immutable;
using Novolis.Storage.Abstractions;

namespace Novolis.Hours.Storage;

/// <summary>Adapts the normal repository provider to administrator-created workplaces.</summary>
public sealed class RepositoryHoursCustomerStore(IRepository<HoursCustomerDocument> repository) : IHoursCustomerStore
{
    private readonly IRepository<HoursCustomerDocument> repository =
        repository ?? throw new ArgumentNullException(nameof(repository));

    /// <inheritdoc />
    public HoursCustomerDocument? FindByOrganisationId(string organisationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organisationId);
        return repository.All().SingleOrDefault(item =>
            string.Equals(item.OrganisationId, organisationId, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public ImmutableArray<HoursCustomerDocument> List() =>
        repository.All()
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray();

    /// <inheritdoc />
    public ValueTask SaveAsync(HoursCustomerDocument customer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customer);
        return repository.UpsertAsync(customer, cancellationToken);
    }
}
