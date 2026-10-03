namespace Novolis.Hours.Domain;

/// <summary>Resolves the immutable profile, template, settings, calendar, and legal preset effective for one employee.</summary>
public interface IHoursPolicyProvider
{
    /// <summary>Gets the policy snapshot used when registering or reviewing one employee's worktime.</summary>
    HoursPolicy GetPolicy(string employeeId);
}
