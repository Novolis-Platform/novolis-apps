using Novolis.Time.Workday;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>Presents a WorkCalendarStack as the workday calendar contract.</summary>
public sealed class DayShapeWorkdayCalendar : IWorkdayCalendar
{
    private readonly WorkCalendarStack stack;

    /// <summary>Initializes an adapter over an already stacked DayShape source.</summary>
    public DayShapeWorkdayCalendar(string id, string countryCode, WorkCalendarStack stack)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentNullException.ThrowIfNull(stack);

        Id = id;
        Source = new WorkdaySourceMetadata(
            NationalHolidayCatalog.SourcePackage,
            NationalHolidayCatalog.SourcePackageVersion,
            countryCode.ToUpperInvariant(),
            null);
        this.stack = stack;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public WorkdaySourceMetadata Source { get; }

    /// <inheritdoc />
    public bool IsWorkday(DateOnly date) => stack.GetDayShape(date).IsWorkingDay;

    /// <inheritdoc />
    public string? GetNonWorkdayReason(DateOnly date)
    {
        var shape = stack.GetDayShape(date);
        if (shape.IsWorkingDay)
        {
            return null;
        }

        var holiday = shape.Tags.FirstOrDefault(tag =>
            tag.Key.Equals("PublicHoliday", StringComparison.Ordinal));
        if (holiday is not null)
        {
            return holiday.Value;
        }

        return date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
            ? "Weekend"
            : "Non-working day";
    }
}
