namespace Novolis.Hours.Client.Presentation;

/// <summary>Session facts shared by every Hours host.</summary>
public sealed class HoursSessionModel
{
    /// <summary>Absolute Hours service URL.</summary>
    public string ServiceUrl { get; set; } = "https://localhost:5700/";

    /// <summary>Signed-in employee identifier.</summary>
    public string? EmployeeId { get; set; }

    /// <summary>Signed-in display name.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Organisation shown in chrome.</summary>
    public string? OrganisationId { get; set; }

    /// <summary>IANA zone from the current WorkDay snapshot.</summary>
    public string TimeZoneId { get; set; } = "Europe/Oslo";

    /// <summary>Whether a person is signed in.</summary>
    public bool IsSignedIn => !string.IsNullOrWhiteSpace(EmployeeId);
}
