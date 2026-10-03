using System.Collections.Immutable;
using Novolis.Storage.Abstractions;

namespace Novolis.Hours.Storage;

/// <summary>Persistent product-role directory kept separate from authentication credentials.</summary>
public sealed class HoursUserDirectory
{
    private readonly IRepository<HoursUserDocument> repository;

    /// <summary>Initializes the directory over the configured JSON repository provider.</summary>
    public HoursUserDirectory(IRepository<HoursUserDocument> repository)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>Finds the product profile for one authenticated identity.</summary>
    public ValueTask<HoursUserDocument?> FindAsync(Guid identityId, CancellationToken cancellationToken = default) =>
        repository.TryGetAsync(identityId, cancellationToken);

    /// <summary>Finds the product profile and worktime configuration for an employment identifier.</summary>
    public HoursUserDocument? FindByEmployeeId(string employeeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        return repository.All().SingleOrDefault(item =>
            string.Equals(item.EmployeeId, employeeId, StringComparison.Ordinal));
    }

    /// <summary>Lists the registered product profiles in deterministic display order.</summary>
    public ImmutableArray<HoursUserDocument> List() =>
        repository.All()
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray();

    /// <summary>Creates or replaces a product profile after its security identity has been created.</summary>
    public ValueTask SaveAsync(HoursUserDocument user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        return repository.UpsertAsync(user, cancellationToken);
    }
}
