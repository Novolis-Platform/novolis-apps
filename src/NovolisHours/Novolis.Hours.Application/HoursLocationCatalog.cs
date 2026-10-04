namespace Novolis.Hours.Application;

/// <summary>Locations a platform operator picks when opening a customer. Not product “patterns”.</summary>
public static class HoursLocationCatalog
{
    /// <summary>United Kingdom — English holidays, Europe/London.</summary>
    public const string UnitedKingdom = "united-kingdom";

    /// <summary>Norway — Norwegian holidays, Europe/Oslo.</summary>
    public const string Norway = "norway";

    /// <summary>France — French holidays, Europe/Paris.</summary>
    public const string France = "france";

    /// <summary>Poland — Polish holidays, Europe/Warsaw.</summary>
    public const string Poland = "poland";

    /// <summary>Finland — Finnish holidays, Europe/Helsinki.</summary>
    public const string Finland = "finland";

    /// <summary>United States — federal holidays, America/Los_Angeles.</summary>
    public const string UnitedStates = "united-states";

    /// <summary>Canada — federal holidays, America/Toronto.</summary>
    public const string Canada = "canada";

    /// <summary>Japan — national holidays, Asia/Tokyo.</summary>
    public const string Japan = "japan";

    /// <summary>Germany — public holidays, Europe/Berlin.</summary>
    public const string Germany = "germany";

    /// <summary>Locations shown on platform customer create.</summary>
    public static IReadOnlyList<HoursLocation> All { get; } =
    [
        new(
            UnitedKingdom,
            "United Kingdom",
            "GB",
            "Europe/London",
            "English public holidays. Default week is Monday to Friday.",
            HoursWorkplaceTemplates.GameRetailRigid),
        new(
            Norway,
            "Norway",
            "NO",
            "Europe/Oslo",
            "Norwegian public holidays. Default week is Monday to Friday.",
            HoursWorkplaceTemplates.NordvikOfficeFlex),
        new(
            France,
            "France",
            "FR",
            "Europe/Paris",
            "French public holidays. Default week is Monday to Friday.",
            HoursCustomerCatalog.AtelierCurie),
        new(
            Poland,
            "Poland",
            "PL",
            "Europe/Warsaw",
            "Polish public holidays. Default week is Monday to Friday.",
            HoursCustomerCatalog.WarsawSettlement),
        new(
            Finland,
            "Finland",
            "FI",
            "Europe/Helsinki",
            "Finnish public holidays. Default week is Monday to Friday.",
            HoursCustomerCatalog.HelsinkiFlex),
        new(
            UnitedStates,
            "United States",
            "US",
            "America/Los_Angeles",
            "US federal holidays. FLSA overtime after 40 hours. California also has daily overtime after 8.",
            HoursCustomerCatalog.PacificYard),
        new(
            Canada,
            "Canada",
            "CA",
            "America/Toronto",
            "Canadian public holidays. Ontario overtime after 44 hours. 30-minute break after 5 hours.",
            HoursCustomerCatalog.TorontoYard),
        new(
            Japan,
            "Japan",
            "JP",
            "Asia/Tokyo",
            "Japanese national holidays. 8 hours a day, 40 a week. Overtime needs an Article 36 agreement.",
            HoursCustomerCatalog.TokyoFlex),
        new(
            Germany,
            "Germany",
            "DE",
            "Europe/Berlin",
            "German public holidays. 8-hour working day, 11-hour rest, break after 6 hours.",
            HoursCustomerCatalog.BerlinOffice),
    ];

    /// <summary>Resolves the catalog template used as the legal and holiday baseline.</summary>
    public static HoursLocation Require(string locationId) =>
        All.FirstOrDefault(item => item.Id.Equals(locationId, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentOutOfRangeException(nameof(locationId), locationId, "Choose a location.");

    /// <summary>Applies commercial defaults: the location owns the calendar, the customer owns the rules.</summary>
    public static HoursCustomer CreateCustomer(string locationId, string organisationId, string displayName)
    {
        var location = Require(locationId);
        var seeded = HoursCustomerCatalog.TryGet(location.TemplateId, out var prototype)
            ? prototype with
            {
                Id = organisationId.Trim(),
                DisplayName = displayName.Trim(),
            }
            : HoursWorkplaceTemplates.Create(location.TemplateId, organisationId, displayName);
        return seeded with
        {
            CountryCode = location.CountryCode,
            TimeZoneId = location.TimeZoneId,
            SaturdayIsWorkingDay = false,
            SevenDayOperation = false,
            AllowsFlex = false,
            AllowsDispute = false,
            AttendanceConfirmationOnly = false,
            ReviewPolicyId = HoursReviewWorkflowCatalog.AttendanceHr,
        };
    }
}
