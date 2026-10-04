namespace Novolis.Hours.Contracts;

/// <summary>Employee usual working clock. Used by Worked as planned.</summary>
public sealed record UpdateUsualHoursRequest(TimeOnly Start, TimeOnly End);
