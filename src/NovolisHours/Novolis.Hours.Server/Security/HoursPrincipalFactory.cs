using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Novolis.Hours.Domain;
using Novolis.Hours.Storage;
using Novolis.Security.Authentication;

namespace Novolis.Hours.Server;

/// <summary>Creates and reads authenticated browser principals without placing authorization data in credential storage.</summary>
public static class HoursPrincipalFactory
{
    /// <summary>Creates a cookie principal from a real Novolis Security session and product profile.</summary>
    public static ClaimsPrincipal Create(HoursUserDocument user, IdentityId identityId, string sessionId) =>
        Create(user, identityId.Value, sessionId, isDemo: false);

    /// <summary>Creates the explicit local demo principal.</summary>
    public static ClaimsPrincipal CreateDemo() =>
        Create(HoursDemoAccount.User, HoursDemoAccount.IdentityId, "demo", isDemo: true);

    /// <summary>Converts a validated principal into the immutable domain actor used by journal events.</summary>
    public static HoursActor ToActor(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var employeeId = principal.FindFirstValue(HoursClaimTypes.EmployeeId)
            ?? throw new UnauthorizedAccessException("The session does not include an employee identifier.");
        var displayName = principal.Identity?.Name ?? employeeId;
        var roleText = principal.FindFirstValue(HoursClaimTypes.Role)
            ?? throw new UnauthorizedAccessException("The session does not include an Hours role.");
        if (!Enum.TryParse<HoursActorRole>(roleText, ignoreCase: true, out var role))
        {
            throw new UnauthorizedAccessException("The session contains an unknown Hours role.");
        }

        return new HoursActor(employeeId, displayName, role);
    }

    private static ClaimsPrincipal Create(
        HoursUserDocument user,
        Guid identityId,
        string sessionId,
        bool isDemo)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, identityId.ToString("D")),
            new(HoursClaimTypes.IdentityId, identityId.ToString("D")),
            new(ClaimTypes.Name, user.DisplayName),
            new(HoursClaimTypes.EmployeeId, user.EmployeeId),
            new(HoursClaimTypes.Role, user.Role.ToString()),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(HoursClaimTypes.SessionId, sessionId),
        };
        if (isDemo)
        {
            claims.Add(new Claim(HoursClaimTypes.Demo, bool.TrueString));
        }

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme,
            ClaimTypes.Name,
            ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }
}
