using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Novolis.Hours.Contracts;

namespace Novolis.Hours.Client.Presentation;

/// <summary>Platform form to open a customer: location, name, and the customer administrator.</summary>
public sealed class CustomerOpenModel
{
    /// <summary>Builds the form from host locations.</summary>
    public CustomerOpenModel(IReadOnlyList<HoursLocationResponse> locations)
    {
        ArgumentNullException.ThrowIfNull(locations);
        Locations = locations.ToImmutableArray();
        SelectedLocationId = Locations.Length > 0 ? Locations[0].Id : string.Empty;
    }

    /// <summary>Country locations that set the public calendar.</summary>
    public ImmutableArray<HoursLocationResponse> Locations { get; }

    /// <summary>Selected location id.</summary>
    public string SelectedLocationId { get; set; }

    /// <summary>Customer slug. Filled from the display name when left empty.</summary>
    public string OrganisationId { get; set; } = string.Empty;

    /// <summary>Customer trading name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Administrator employment id and login default.</summary>
    public string AdminEmployeeId { get; set; } = string.Empty;

    /// <summary>Administrator login.</summary>
    public string AdminLogin { get; set; } = string.Empty;

    /// <summary>Administrator display name.</summary>
    public string AdminDisplayName { get; set; } = string.Empty;

    /// <summary>Administrator password.</summary>
    public string AdminPassword { get; set; } = "Workplace-2026-Strong!";

    /// <summary>When true the administrator also closes months as HR.</summary>
    public bool AdminAlsoHr { get; set; }

    /// <summary>When true the administrator also records a personal week.</summary>
    public bool AdminAlsoEmployee { get; set; }

    /// <summary>Created customer after open succeeds.</summary>
    public HoursCustomerResponse? Created { get; set; }

    /// <summary>Created administrator after open succeeds.</summary>
    public HoursUserResponse? Admin { get; set; }

    /// <summary>Selected location, when known.</summary>
    public HoursLocationResponse? SelectedLocation =>
        Locations.FirstOrDefault(item =>
            item.Id.Equals(SelectedLocationId, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether the form can be submitted.</summary>
    public bool CanOpen =>
        !string.IsNullOrWhiteSpace(ResolvedOrganisationId) &&
        !string.IsNullOrWhiteSpace(DisplayName) &&
        !string.IsNullOrWhiteSpace(SelectedLocationId) &&
        !string.IsNullOrWhiteSpace(ResolvedAdminEmployeeId) &&
        !string.IsNullOrWhiteSpace(ResolvedAdminDisplayName) &&
        !string.IsNullOrWhiteSpace(AdminPassword);

    /// <summary>Organisation id after slug fallback.</summary>
    public string ResolvedOrganisationId =>
        string.IsNullOrWhiteSpace(OrganisationId) ? Slug(DisplayName) : OrganisationId.Trim().ToLowerInvariant();

    /// <summary>Administrator id after fallback from login or name.</summary>
    public string ResolvedAdminEmployeeId =>
        string.IsNullOrWhiteSpace(AdminEmployeeId)
            ? Slug(string.IsNullOrWhiteSpace(AdminLogin) ? AdminDisplayName : AdminLogin)
            : AdminEmployeeId.Trim();

    /// <summary>Administrator login after fallback.</summary>
    public string ResolvedAdminLogin =>
        string.IsNullOrWhiteSpace(AdminLogin) ? ResolvedAdminEmployeeId : AdminLogin.Trim();

    /// <summary>Administrator display name after fallback.</summary>
    public string ResolvedAdminDisplayName =>
        string.IsNullOrWhiteSpace(AdminDisplayName) ? ResolvedAdminLogin : AdminDisplayName.Trim();

    /// <summary>Division seed for the first administrator.</summary>
    public string DivisionId =>
        SelectedLocation?.CountryCode is "GB" ? "division-uk" : "division-north";

    /// <summary>Team seed for the first administrator.</summary>
    public string TeamId =>
        SelectedLocation?.CountryCode is "GB" ? "team-high-street" : "team-a";

    /// <summary>Lowercase hyphenated slug from a commercial name.</summary>
    public static string Slug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var pendingHyphen = false;
        foreach (var ch in value.Trim().Normalize(NormalizationForm.FormD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(ch))
            {
                if (pendingHyphen && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(ch));
                pendingHyphen = false;
                continue;
            }

            pendingHyphen = builder.Length > 0;
        }

        return builder.ToString();
    }
}
