using Novolis.Hours.Domain;
using Novolis.Hours.Storage;

namespace Novolis.Hours.Server;

/// <summary>Explicitly local demo principal used only when the opt-in demo credential setting is enabled.</summary>
public static class HoursServerDemoAccount
{
    /// <summary>Stable non-production demo identity.</summary>
    public static Guid IdentityId { get; } = Guid.Parse("05d98e16-4c57-4e34-a769-2a6198513090");

    /// <summary>Gets the documented local login name.</summary>
    public const string Login = "admin";

    /// <summary>Gets the documented local login password.</summary>
    public const string Password = "admin";

    /// <summary>Gets the product authorization profile for the demo account.</summary>
    public static HoursUserDocument User { get; } = new(
        IdentityId,
        "admin",
        "admin",
        "Demo administrator",
        HoursActorRole.Administrator);
}
