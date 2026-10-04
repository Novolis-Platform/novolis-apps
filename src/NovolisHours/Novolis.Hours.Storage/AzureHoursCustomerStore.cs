using System.Collections.Immutable;
using Novolis.Storage.Abstractions;

namespace Novolis.Hours.Storage;

/// <summary>Hours workplace store backed by scalar Azure Table rows.</summary>
public sealed class AzureHoursCustomerStore(IRepository<AzureHoursCustomerRecord> repository) : IHoursCustomerStore
{
    private readonly IRepository<AzureHoursCustomerRecord> repository =
        repository ?? throw new ArgumentNullException(nameof(repository));

    /// <inheritdoc />
    public HoursCustomerDocument? FindByOrganisationId(string organisationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organisationId);
        return repository.All()
            .SingleOrDefault(item =>
                string.Equals(item.OrganisationId, organisationId, StringComparison.OrdinalIgnoreCase))
            ?.ToDocument();
    }

    /// <inheritdoc />
    public ImmutableArray<HoursCustomerDocument> List() =>
        repository.All()
            .Select(item => item.ToDocument())
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray();

    /// <inheritdoc />
    public ValueTask SaveAsync(HoursCustomerDocument customer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customer);
        return repository.UpsertAsync(AzureHoursCustomerRecord.FromDocument(customer), cancellationToken);
    }
}
