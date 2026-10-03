using Novolis.Hours.Domain;
using Novolis.Time.Worktime;
using Novolis.Time.Worktime.Legal;

namespace Novolis.Hours.Contracts;

/// <summary>Stable JSON API contracts shared by the Hours host and clients.</summary>
public sealed record AntiforgeryResponse(string Token);
