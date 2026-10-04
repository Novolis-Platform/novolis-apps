using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Novolis.Hours.Domain;
using Novolis.Hours.Storage;
using Novolis.Security.Authentication;

namespace Novolis.Hours.Server;

/// <summary>Creates and reads authenticated browser principals without placing authorization data in credential storage.</summary>
public static class HoursServerPrincipalFactory
{
    /// <summary>Creates a cookie principal from a real Novolis Security session and product profile.</summary>
    public static ClaimsPrincipal Create(HoursUserDocument user, IdentityId identityId, string sessionId) =>
        Create(user, identityId.Value, sessionId, isDemo: false);

    /// <summary>Creates the explicit local demo principal.</summary>
    public static ClaimsPrincipal CreateDemo() =>
        Create(HoursServerDemoAccount.User, HoursServerDemoAccount.IdentityId, "demo", isDemo: true);

    /// <summary>Converts a validated principal into the immutable domain actor used by journal events.</summary>
    public static HoursActor ToActor(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var employeeId = principal.FindFirstValue(HoursServerClaimTypes.EmployeeId)
            ?? throw new UnauthorizedAccessException("The session does not include an employee identifier.");
        var displayName = principal.Identity?.Name ?? employeeId;
        var roleText = principal.FindFirstValue(HoursServerClaimTypes.Role)
            ?? throw new UnauthorizedAccessException("The session does not include an Hours role.");
        if (!Enum.TryParse<HoursActorRole>(roleText, ignoreCase: true, out var role))
        {
            throw new UnauthorizedAccessException("The session contains an unknown Hours role.");
        }

        return new HoursActor(employeeId, displayName, role);
    }

    /// <summary>Every Hours role claim on the session, primary first.</summary>
    public static IReadOnlyList<HoursActorRole> AssignedRoles(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var roles = new List<HoursActorRole>();
        foreach (var claim in principal.FindAll(HoursServerClaimTypes.Role))
        {
            if (Enum.TryParse<HoursActorRole>(claim.Value, ignoreCase: true, out var role) &&
                !roles.Contains(role))
            {
                roles.Add(role);
            }
        }

        return roles;
    }

    /// <summary>Whether the session holds <paramref name="role"/>.</summary>
    public static bool HasRole(ClaimsPrincipal principal, HoursActorRole role) =>
        AssignedRoles(principal).Contains(role);


    private static ClaimsPrincipal Create(
        HoursUserDocument user,
        Guid identityId,
        string sessionId,
        bool isDemo)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, identityId.ToString("D")),
            new(HoursServerClaimTypes.IdentityId, identityId.ToString("D")),
            new(ClaimTypes.Name, user.DisplayName),
            new(HoursServerClaimTypes.EmployeeId, user.EmployeeId),
            new(HoursServerClaimTypes.SessionId, sessionId),
        };
        foreach (var role in user.AssignedRoles)
        {
            claims.Add(new Claim(HoursServerClaimTypes.Role, role.ToString()));
            claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
        }
        if (isDemo)
        {
            claims.Add(new Claim(HoursServerClaimTypes.Demo, bool.TrueString));
        }

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme,
            ClaimTypes.Name,
            ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }
}
