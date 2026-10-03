using Novolis.Time.Worktime;

namespace Novolis.Hours.Domain;

/// <summary>Projects a reusable tenant policy into a distinct immutable employment settings snapshot per employee.</summary>
public sealed class FixedHoursPolicyProvider : IHoursPolicyProvider
{
    private readonly HoursPolicy baseline;

    /// <summary>Initializes the provider with the configured tenant baseline.</summary>
    public FixedHoursPolicyProvider(HoursPolicy baseline)
    {
        this.baseline = baseline ?? throw new ArgumentNullException(nameof(baseline));
    }

    /// <inheritdoc />
    public HoursPolicy GetPolicy(string employeeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        if (string.Equals(baseline.EmploymentSettings.EmploymentId, employeeId, StringComparison.Ordinal))
        {
            return baseline;
        }

        var settings = baseline.EmploymentSettings;
        return baseline with
        {
            EmploymentSettings = new EmploymentSettings(
                employeeId,
                settings.Profile,
                settings.Calendar,
                settings.Template,
                settings.WorkFraction,
                settings.ExpectedIntervalOverride),
        };
    }
}
