using System.Collections.Immutable;
using Novolis.Hours.Domain;

namespace Novolis.Hours.Storage;

/// <summary>Persistence boundary for product-role profiles independent of the selected storage provider.</summary>
public interface IHoursUserStore
{
    /// <summary>Finds the product profile linked to an authentication identity.</summary>
    ValueTask<HoursUserDocument?> FindAsync(
        Guid identityId,
        CancellationToken cancellationToken = default);

    /// <summary>Finds a profile by its employment identifier.</summary>
    HoursUserDocument? FindByEmployeeId(string employeeId);

    /// <summary>Lists profiles in deterministic display order.</summary>
    ImmutableArray<HoursUserDocument> List();

    /// <summary>Creates or replaces a product profile.</summary>
    ValueTask SaveAsync(
        HoursUserDocument user,
        CancellationToken cancellationToken = default);
}
