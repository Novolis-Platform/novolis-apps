using Novolis.Hours.Domain;
using Novolis.Time.Worktime;
using Novolis.Time.Worktime.Legal;

namespace Novolis.Hours.Contracts;

/// <summary>Calendar identity used by the effective policy.</summary>
public sealed record CalendarResponse(string Id, string Source, string Version, string CountryCode);
