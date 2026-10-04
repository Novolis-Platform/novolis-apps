namespace Novolis.Hours.Domain.Review;

/// <summary>Small deterministic business-day calculator for review deadlines.</summary>
public static class BusinessDayCalculator
{
    /// <summary>Adds weekdays, without treating a deadline as a lockout.</summary>
    public static DateOnly AddBusinessDays(DateOnly date, int businessDays)
    {
        if (businessDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(businessDays));
        }

        var result = date;
        var remaining = businessDays;
        while (remaining > 0)
        {
            result = result.AddDays(1);
            if (IsBusinessDay(result))
            {
                remaining--;
            }
        }

        return result;
    }

    /// <summary>Returns whether a date is a weekday.</summary>
    public static bool IsBusinessDay(DateOnly date) =>
        date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
}
