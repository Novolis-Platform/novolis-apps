using System.Collections.Immutable;

namespace Novolis.Hours.Storage;

/// <summary>Persistent product-role directory kept separate from authentication credentials.</summary>
public sealed class HoursUserDirectory(IHoursUserStore store)
{
    private readonly IHoursUserStore store =
        store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>Finds the product profile for one authenticated identity.</summary>
    public ValueTask<HoursUserDocument?> FindAsync(Guid identityId, CancellationToken cancellationToken = default) =>
        store.FindAsync(identityId, cancellationToken);

    /// <summary>Finds the product profile and worktime configuration for an employment identifier.</summary>
    public HoursUserDocument? FindByEmployeeId(string employeeId) =>
        store.FindByEmployeeId(employeeId);

    /// <summary>Lists the registered product profiles in deterministic display order.</summary>
    public ImmutableArray<HoursUserDocument> List() => store.List();

    /// <summary>Creates or replaces a product profile after its security identity has been created.</summary>
    public ValueTask SaveAsync(HoursUserDocument user, CancellationToken cancellationToken = default) =>
        store.SaveAsync(user, cancellationToken);
}
