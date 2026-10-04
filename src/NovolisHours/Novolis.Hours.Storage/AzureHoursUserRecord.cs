using System.Globalization;
using Novolis.Hours.Domain;
using Novolis.Storage.Abstractions;

namespace Novolis.Hours.Storage;

/// <summary>
/// Scalar Azure Table row for an Hours product profile.
/// Authentication credentials remain in the separate Novolis Security rows.
/// </summary>
public sealed class AzureHoursUserRecord : IHasId
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <summary>Employment identifier used by the Hours domain.</summary>
    public string EmployeeId { get; set; } = string.Empty;

    /// <summary>Login display hint; authentication still owns credential verification.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Human-readable display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Serialized product role.</summary>
    public string Role { get; set; } = nameof(HoursActorRole.Employee);

    /// <summary>Optional legal preset override.</summary>
    public string? LegalPresetId { get; set; }

    /// <summary>Employment fraction stored as an Azure Table double.</summary>
    public double WorkFraction { get; set; } = 1d;

    /// <summary>Optional expected-interval start in invariant <c>HH:mm:ss</c> form.</summary>
    public string? ExpectedIntervalOverrideStart { get; set; }

    /// <summary>Optional expected-interval end in invariant <c>HH:mm:ss</c> form.</summary>
    public string? ExpectedIntervalOverrideEnd { get; set; }

    /// <summary>Organisation scope used by acceptance authorization.</summary>
    public string OrganisationId { get; set; } = "norway-org";

    /// <summary>Optional division scope.</summary>
    public string? DivisionId { get; set; }

    /// <summary>Optional team scope.</summary>
    public string? TeamId { get; set; }

    /// <summary>Optional manager approval level.</summary>
    public int? ApprovalLevel { get; set; }

    /// <summary>Creates a scalar Azure row from the product document.</summary>
    public static AzureHoursUserRecord FromDocument(HoursUserDocument document) =>
        new()
        {
            Id = document.Id,
            EmployeeId = document.EmployeeId,
            Login = document.Login,
            DisplayName = document.DisplayName,
            Role = document.Role.ToString(),
            LegalPresetId = document.LegalPresetId,
            WorkFraction = (double)document.WorkFraction,
            ExpectedIntervalOverrideStart = FormatTime(document.ExpectedIntervalOverrideStart),
            ExpectedIntervalOverrideEnd = FormatTime(document.ExpectedIntervalOverrideEnd),
            OrganisationId = document.OrganisationId,
            DivisionId = document.DivisionId,
            TeamId = document.TeamId,
            ApprovalLevel = document.ApprovalLevel,
        };

    /// <summary>Rehydrates the immutable product document from an Azure row.</summary>
    public HoursUserDocument ToDocument()
    {
        if (!Enum.TryParse<HoursActorRole>(Role, ignoreCase: false, out var role))
        {
            throw new InvalidOperationException(
                $"Hours Azure user row '{Id}' contains unknown role '{Role}'.");
        }

        return new HoursUserDocument(
            Id,
            EmployeeId,
            Login,
            DisplayName,
            role,
            LegalPresetId,
            Convert.ToDecimal(WorkFraction, CultureInfo.InvariantCulture),
            ParseTime(ExpectedIntervalOverrideStart),
            ParseTime(ExpectedIntervalOverrideEnd),
            OrganisationId,
            DivisionId,
            TeamId,
            ApprovalLevel);
    }

    private static string? FormatTime(TimeOnly? value) =>
        value?.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    private static TimeOnly? ParseTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return TimeOnly.Parse(value, CultureInfo.InvariantCulture);
    }
}
