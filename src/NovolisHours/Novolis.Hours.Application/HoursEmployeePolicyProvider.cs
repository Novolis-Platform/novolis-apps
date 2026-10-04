using Novolis.Hours.Domain;
using Novolis.Hours.Storage;
using Novolis.Time;
using Novolis.Time.Worktime;

namespace Novolis.Hours.Application;

/// <summary>Resolves an immutable effective worktime policy from the tenant baseline and persisted employment settings.</summary>
public sealed class HoursEmployeePolicyProvider : IHoursPolicyProvider
{
    private readonly HoursPolicy tenantBaseline;
    private readonly HoursUserDirectory users;
    private readonly TimeProvider timeProvider;
    private readonly HoursServerOptions options;

    /// <summary>Initializes the policy resolver over the product's persistent user directory.</summary>
    public HoursEmployeePolicyProvider(
        HoursPolicy tenantBaseline,
        HoursUserDirectory users,
        TimeProvider timeProvider,
        HoursServerOptions options)
    {
        this.tenantBaseline = tenantBaseline ?? throw new ArgumentNullException(nameof(tenantBaseline));
        this.users = users ?? throw new ArgumentNullException(nameof(users));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public HoursPolicy GetPolicy(string employeeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        var user = users.FindByEmployeeId(employeeId);
        var policy = CreateTenantPolicy(user?.LegalPresetId);
        var baselineSettings = policy.EmploymentSettings;
        var overrideInterval = CreateOverrideInterval(user);

        return policy with
        {
            EmploymentSettings = new EmploymentSettings(
                employeeId,
                baselineSettings.Profile,
                baselineSettings.Calendar,
                baselineSettings.Template,
                user?.WorkFraction ?? baselineSettings.WorkFraction,
                overrideInterval),
        };
    }

    private HoursPolicy CreateTenantPolicy(string? employeePresetId)
    {
        if (string.IsNullOrWhiteSpace(employeePresetId) ||
            string.Equals(employeePresetId, "hours.platform", StringComparison.OrdinalIgnoreCase))
        {
            return tenantBaseline;
        }

        var year = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).Year;
        var policy = HoursPolicyCatalog.Create(employeePresetId, year);
        return string.IsNullOrWhiteSpace(options.OvertimeAgreementMessage)
            ? policy
            : policy with
            {
                LegalPreset = policy.LegalPreset.WithOvertimeAgreementMessage(options.OvertimeAgreementMessage),
            };
    }

    private static ClockInterval? CreateOverrideInterval(HoursUserDocument? user)
    {
        if (user?.ExpectedIntervalOverrideStart is null && user?.ExpectedIntervalOverrideEnd is null)
        {
            return null;
        }

        if (user.ExpectedIntervalOverrideStart is null || user.ExpectedIntervalOverrideEnd is null)
        {
            throw new InvalidOperationException(
                $"Employee '{user.EmployeeId}' has an incomplete expected-interval override.");
        }

        return new ClockInterval(
            user.ExpectedIntervalOverrideStart.Value,
            user.ExpectedIntervalOverrideEnd.Value);
    }
}
