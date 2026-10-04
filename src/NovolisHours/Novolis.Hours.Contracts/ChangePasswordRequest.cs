using Novolis.Time.Worktime;
using Novolis.Time.Worktime.Legal;

namespace Novolis.Hours.Contracts;

/// <summary>Authenticated password-change request.</summary>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
