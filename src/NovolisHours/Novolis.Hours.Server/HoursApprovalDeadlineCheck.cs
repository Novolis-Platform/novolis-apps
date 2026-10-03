using Novolis.Hours.Domain;

namespace Novolis.Hours.Server;

/// <summary>Workflow payload requesting a transparent deadline assessment for one approval period.</summary>
public sealed record HoursApprovalDeadlineCheck(Guid PeriodId, DateOnly AsOf, HoursActor Actor);
