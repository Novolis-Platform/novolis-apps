namespace Novolis.Hours.Application;

/// <summary>Country working-time stories Hours must be able to record against.</summary>
public static class HoursWorkingTimeNorms
{
    /// <summary>Looks up the statutory story for a location country code.</summary>
    public static HoursWorkingTimeNorm ForCountry(string countryCode) =>
        countryCode.ToUpperInvariant() switch
        {
            "GB" => new(
                "GB",
                "United Kingdom",
                "Shop hours are attendance. Overtime is a local agreement, not a weekly ledger.",
                "Rest breaks follow the shop rota.",
                "Sunday is usually shut.",
                8 * 60,
                48 * 60,
                null),
            "NO" => new(
                "NO",
                "Norway",
                "37.5-hour office week. Surplus is flex, not a cash overtime line.",
                "Lunch is outside the usual clock.",
                "11-hour daily rest from the EU Working Time Directive.",
                450,
                40 * 60,
                null),
            "FR" => new(
                "FR",
                "France",
                "35-hour week with annualisation. A short Tuesday is not a forgotten day.",
                "Lunch splits the usual clock.",
                "11-hour daily rest.",
                7 * 60,
                35 * 60,
                null),
            "PL" => new(
                "PL",
                "Poland",
                "Settlement period. The employer closes the month.",
                "Lunch splits the usual clock.",
                "11-hour daily rest.",
                8 * 60,
                40 * 60,
                null),
            "FI" => new(
                "FI",
                "Finland",
                "Liukuva työaika. Surplus is flex inside the envelope.",
                "Lunch is outside the usual clock.",
                "11-hour daily rest.",
                450,
                40 * 60,
                null),
            "US" => new(
                "US",
                "United States",
                "FLSA: time-and-a-half after 40 hours in a workweek. California also pays daily overtime after 8 hours, and double time after 12.",
                "Federal law does not require a meal. California requires 30 unpaid minutes after 5 hours and a paid 10-minute rest every 4 hours.",
                "No federal daily rest. The workweek is the employer's, often Sunday to Saturday.",
                8 * 60,
                40 * 60,
                8 * 60),
            "CA" => new(
                "CA",
                "Canada",
                "Ontario ESA: overtime after 44 hours in a week. Federal Canada Labour Code uses 40 standard hours and a 48-hour ceiling.",
                "30 unpaid minutes during every 5 consecutive hours (federal). Ontario requires an eating period.",
                "Federal rest is 8 consecutive hours between shifts.",
                8 * 60,
                44 * 60,
                null),
            "JP" => new(
                "JP",
                "Japan",
                "Labour Standards Act: 8 hours a day and 40 a week. Overtime needs a filed Article 36 agreement, usually capped at 45 hours a month and 360 a year.",
                "Breaks sit outside statutory hours.",
                "Flextime is a labour-management agreement, not a painted strip by default.",
                8 * 60,
                40 * 60,
                8 * 60),
            "DE" => new(
                "DE",
                "Germany",
                "Arbeitszeitgesetz: 8 hours a working day, up to 10 when the average stays 8 over 24 weeks.",
                "30 minutes rest after more than 6 hours, 45 after more than 9. No more than 6 hours without a break.",
                "11-hour uninterrupted daily rest.",
                8 * 60,
                48 * 60,
                8 * 60),
            "BE" => new(
                "BE",
                "Belgium",
                "38-hour week is common under a sector CAO. Overtime is exceptional.",
                "Breaks follow the CAO.",
                "11-hour daily rest.",
                456,
                38 * 60,
                null),
            _ => new(
                countryCode,
                countryCode,
                "Record the hours that were worked.",
                "Breaks follow the workplace rules.",
                "Rest follows the workplace rules.",
                8 * 60,
                40 * 60,
                null),
        };
}
