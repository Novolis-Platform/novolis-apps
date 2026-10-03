namespace Novolis.Hours.Server;

/// <summary>Host configuration for local JSON storage and the explicitly development-only demo sign-in.</summary>
public sealed class HoursServerOptions
{
    /// <summary>JSON directory used by the self-contained host.</summary>
    public string DataPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Novolis",
        "Hours");

    /// <summary>Uses a real in-memory journal for feature harnesses instead of the JSON journal.</summary>
    public bool UseInMemoryJournal { get; set; }

    /// <summary>Requires HTTPS for the application surface outside Development.</summary>
    public bool RequireHttps { get; set; } = true;

    /// <summary>Allows the documented local-only demo account; it is disabled unless explicitly requested in Development.</summary>
    public bool EnableDemoAdminCredentials { get; set; }

    /// <summary>Login used only to provision the first non-demo administrator through the normal security path.</summary>
    public string InitialAdministratorLogin { get; set; } = "admin";

    /// <summary>Product employee identifier assigned to the first non-demo administrator.</summary>
    public string InitialAdministratorEmployeeId { get; set; } = "admin";

    /// <summary>Display name assigned to the first non-demo administrator.</summary>
    public string InitialAdministratorDisplayName { get; set; } = "Administrator";

    /// <summary>Secret used only when no real administrator exists; supply it through secure configuration rather than source control.</summary>
    public string? InitialAdministratorPassword { get; set; }

    /// <summary>Stable legal-preset identifier selected for this tenant's starting worktime policy.</summary>
    public string LegalPresetId { get; set; } = "norway.private.flex";

    /// <summary>Optional tenant-specific agreement message shown when financial-compensation hours lack manager agreement.</summary>
    public string? OvertimeAgreementMessage { get; set; }
}
