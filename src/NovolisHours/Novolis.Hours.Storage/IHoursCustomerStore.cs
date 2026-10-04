using System.Collections.Immutable;

namespace Novolis.Hours.Storage;

/// <summary>Persists administrator-created workplaces separately from the built-in catalog.</summary>
public interface IHoursCustomerStore
{
    /// <summary>Finds one persisted workplace by organisation id.</summary>
    HoursCustomerDocument? FindByOrganisationId(string organisationId);

    /// <summary>Lists persisted workplaces.</summary>
    ImmutableArray<HoursCustomerDocument> List();

    /// <summary>Creates or replaces a persisted workplace.</summary>
    ValueTask SaveAsync(HoursCustomerDocument customer, CancellationToken cancellationToken = default);
}
