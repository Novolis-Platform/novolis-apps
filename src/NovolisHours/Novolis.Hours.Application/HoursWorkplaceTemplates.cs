namespace Novolis.Hours.Application;

/// <summary>First-class setup patterns: Norwegian flex office and British Game shop.</summary>
public static class HoursWorkplaceTemplates
{
    /// <summary>Nordvik-style flex office with paint and cascading review.</summary>
    public const string NordvikOfficeFlex = HoursCustomerCatalog.NordvikOffice;

    /// <summary>Game-style rigid shop hours with attendance confirmation and HR close.</summary>
    public const string GameRetailRigid = HoursCustomerCatalog.GameRetail;

    /// <summary>Templates shown on the administrator setup walkthrough.</summary>
    public static IReadOnlyList<HoursWorkplaceTemplate> All { get; } =
    [
        new(
            NordvikOfficeFlex,
            "Nordvik office",
            "Norwegian flex. People paint customer time and submit through a manager ladder. Saturday is a weekend.",
            HoursCustomerCatalog.All.Single(item => item.Id == HoursCustomerCatalog.NordvikOffice)),
        new(
            GameRetailRigid,
            "Game shop",
            "British shop hours. People confirm attendance. Only HR closes. No flex, no overtime, no dispute.",
            HoursCustomerCatalog.All.Single(item => item.Id == HoursCustomerCatalog.GameRetail)),
    ];

    /// <summary>Clones a template into a new workplace id and display name.</summary>
    public static HoursCustomer Create(string templateId, string organisationId, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(organisationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var template = All.FirstOrDefault(item =>
            item.Id.Equals(templateId, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentOutOfRangeException(nameof(templateId), templateId, "Unknown workplace template.");
        return template.Prototype with
        {
            Id = organisationId.Trim(),
            DisplayName = displayName.Trim(),
        };
    }
}
