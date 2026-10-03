namespace Novolis.Hours.Domain;

/// <summary>Read-only application service that executes named queries against replayed worktime facts.</summary>
public sealed class HoursQueryService
{
    private readonly HoursService hours;

    /// <summary>Initializes the query service over the real worktime application service.</summary>
    public HoursQueryService(HoursService hours)
    {
        this.hours = hours ?? throw new ArgumentNullException(nameof(hours));
    }

    /// <summary>Executes a query without modifying the journal.</summary>
    public async ValueTask<HoursQueryResult> ExecuteAsync(
        HoursQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var view = await hours.GetEmployeeViewAsync(query.EmployeeId, cancellationToken);
        return HoursQueryEngine.Execute(view, query);
    }
}
