using System.Text.Json.Serialization;

namespace Novolis.Hours.Domain.Work;

/// <summary>Identifies one logical employee workday without imposing a midnight boundary.</summary>
public readonly record struct WorkDayKey
{
    /// <summary>Initializes a logical workday key.</summary>
    [JsonConstructor]
    public WorkDayKey(string employeeId, DateOnly nominalDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        EmployeeId = employeeId;
        NominalDate = nominalDate;
    }

    /// <summary>Employee whose work is being described.</summary>
    public string EmployeeId { get; }

    /// <summary>Local nominal date to which the work belongs.</summary>
    public DateOnly NominalDate { get; }
}
