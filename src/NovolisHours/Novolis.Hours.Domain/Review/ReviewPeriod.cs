namespace Novolis.Hours.Domain.Review;

/// <summary>Immutable date range presented for mutual review.</summary>
public sealed record ReviewPeriod
{
    /// <summary>Initializes a review period.</summary>
    public ReviewPeriod(Guid id, string employeeId, DateOnly from, DateOnly through)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A review period requires an identity.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        if (through < from)
        {
            throw new ArgumentException(
                "The period end must not precede the period start.",
                nameof(through));
        }

        Id = id;
        EmployeeId = employeeId;
        From = from;
        Through = through;
    }

    /// <summary>Period identity.</summary>
    public Guid Id { get; }

    /// <summary>Employee whose records are presented.</summary>
    public string EmployeeId { get; }

    /// <summary>Inclusive start date.</summary>
    public DateOnly From { get; }

    /// <summary>Inclusive end date.</summary>
    public DateOnly Through { get; }

    /// <summary>Creates a period with a new identity.</summary>
    public static ReviewPeriod Create(
        string employeeId,
        DateOnly from,
        DateOnly through) =>
        new(Guid.CreateVersion7(), employeeId, from, through);
}
