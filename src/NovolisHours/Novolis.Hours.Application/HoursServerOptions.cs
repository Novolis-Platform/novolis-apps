namespace Novolis.Hours.Application;

/// <summary>Host configuration for storage, client origins, and the explicitly development-only demo sign-in.</summary>
public sealed class HoursServerOptions
{
    /// <summary>Storage provider name: <c>json</c>, <c>azure-tables</c>, or <c>in-memory</c>.</summary>
    public string StorageProvider { get; set; } = "json";

    /// <summary>JSON directory used by the self-contained host.</summary>
    public string DataPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Novolis",
        "Hours");

    /// <summary>
    /// Azure Table connection string, Azurite connection string, or https table endpoint.
    /// An absolute endpoint uses the host's Azure credential instead of a shared key.
    /// </summary>
    public string? AzureTablesConnectionString { get; set; }

    /// <summary>Prefix used to isolate this Hours tenant's Azure Tables.</summary>
    public string AzureTablesTablePrefix { get; set; } = "novolis-hours";

    /// <summary>Uses a real in-memory journal for feature harnesses instead of the JSON journal.</summary>
    public bool UseInMemoryJournal { get; set; }

    /// <summary>Requires HTTPS for the application surface outside Development.</summary>
    public bool RequireHttps { get; set; } = true;

    /// <summary>
    /// Proxy addresses allowed to supply forwarded scheme and client-address headers.
    /// <c>*</c> trusts any proxy, for a platform ingress that is the only path to the container.
    /// </summary>
    public string[] TrustedProxyAddresses { get; set; } = ["127.0.0.1", "::1"];

    /// <summary>Origins allowed to call the protected API from a standalone browser client.</summary>
    public string[] AllowedClientOrigins { get; set; } = [];

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

    /// <summary>Enables the development-only named acceptance identities and configuration catalog.</summary>
    public bool EnableAcceptanceSeed { get; set; }

    /// <summary>Password used for development-only acceptance identities.</summary>
    public string? AcceptanceSeedPassword { get; set; }

    /// <summary>Stable legal-preset identifier selected for this tenant's starting worktime policy.</summary>
    public string LegalPresetId { get; set; } = "norway.private.flex";

    /// <summary>Optional tenant-specific agreement message shown when financial-compensation hours lack manager agreement.</summary>
    public string? OvertimeAgreementMessage { get; set; }
}
