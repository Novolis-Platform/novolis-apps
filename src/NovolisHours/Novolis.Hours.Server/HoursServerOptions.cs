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

    /// <summary>Allows the documented admin/admin demo bootstrap account; disable this before any non-demo deployment.</summary>
    public bool EnableDemoAdminCredentials { get; set; } = true;

    /// <summary>Configures whether the host should seed a sample employee and worktime facts on first use.</summary>
    public bool SeedSampleData { get; set; } = true;

    /// <summary>Stable legal-preset identifier selected for this tenant's starting worktime policy.</summary>
    public string LegalPresetId { get; set; } = "norway.private.flex";

    /// <summary>Optional tenant-specific agreement message shown when financial-compensation hours lack manager agreement.</summary>
    public string? OvertimeAgreementMessage { get; set; }
}
