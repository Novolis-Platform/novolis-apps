namespace Novolis.Hours.Server;

/// <summary>Product claim names carried by the encrypted browser session.</summary>
public static class HoursClaimTypes
{
    /// <summary>Employee identifier used by the worktime journal.</summary>
    public const string EmployeeId = "novolis-hours/employee-id";

    /// <summary>Product role name.</summary>
    public const string Role = "novolis-hours/role";

    /// <summary>Novolis Security session identifier.</summary>
    public const string SessionId = "novolis-hours/session-id";

    /// <summary>Marks the limited local demo account.</summary>
    public const string Demo = "novolis-hours/demo";
}
