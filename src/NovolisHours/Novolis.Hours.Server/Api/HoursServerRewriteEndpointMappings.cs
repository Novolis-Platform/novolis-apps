using System.Collections.Immutable;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Novolis.Hours.Application;
using Novolis.Hours.Contracts;
using Novolis.Hours.Domain;
using Novolis.Hours.Domain.Configuration;
using Novolis.Hours.Domain.Dimensions;
using Novolis.Hours.Domain.Review;
using Novolis.Hours.Domain.Work;
using Novolis.Hours.Storage;
using DomainWorkSource = Novolis.Hours.Domain.Work.WorkRecordSource;

namespace Novolis.Hours.Server;

/// <summary>Maps the immutable v2 work-registration and audit API.</summary>
public static class HoursServerRewriteEndpointMappings
{
    /// <summary>Adds secured v2 work and audit routes without replacing legacy routes.</summary>
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v2").RequireAuthorization();
        api.MapPost("/work-registrations", RecordWorkRegistrationAsync);
        api.MapPost(
            "/employees/{employeeId}/dimension-assignments",
            RecordDimensionAssignmentAsync);
        api.MapPost(
            "/employees/{employeeId}/configuration/publications",
            PublishConfigurationAsync);
        api.MapGet(
            "/employees/{employeeId}/work-registrations",
            ReadWorkRegistrationsAsync);
        api.MapGet(
            "/employees/{employeeId}/audit",
            ReadAuditAsync);
        api.MapGet(
            "/employees/{employeeId}/workdays/{date}",
            ReadWorkDayAsync);
        api.MapGet(
            "/employees/{employeeId}/workdays",
            ReadWorkDaysAsync);
        api.MapGet("/reviews/{periodId:guid}", ReadReviewAsync);
        api.MapPost("/reviews", CreateReviewPeriodAsync);
        api.MapPost("/reviews/{periodId:guid}/actions", RecordReviewActionAsync);
        api.MapGet("/reports/health", ReadHealthReportAsync);
        api.MapGet("/reports/business-pressure", ReadBusinessPressureReportAsync);
    }

    private static async Task<IResult> RecordWorkRegistrationAsync(
        RecordWorkRegistrationRequest request,
        HttpContext context,
        WorkRegistrationService registrations,
        WorkLedgerService ledger,
        HoursAcceptanceConfiguration configuration,
        HoursUserDirectory users,
        IHoursJournal journal)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = HoursServerPrincipalFactory.ToActor(context.User);
        if (!CanRecord(actor, request.EmployeeId, users, HoursContractMapping.ToDomain(request.Source)))
        {
            return Results.Forbid();
        }
        if (string.IsNullOrWhiteSpace(request.EmployeeId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["employeeId"] = ["An employee identifier is required."],
            });
        }

        if (request.Intent == WorkRegistrationIntent.WorkedAsScheduled &&
            !request.Intervals.IsDefaultOrEmpty)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["intervals"] = ["A scheduled assertion must not contain clock observations."],
            });
        }

        if (request.Intent != WorkRegistrationIntent.WorkedAsScheduled &&
            request.Intervals.IsDefaultOrEmpty)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["intervals"] = ["A manual assertion requires at least one interval."],
            });
        }

        var source = HoursContractMapping.ToDomain(request.Source) ?? (actor.Role == HoursActorRole.Employee
            ? DomainWorkSource.Employee
            : actor.Role == HoursActorRole.System
                ? DomainWorkSource.Integration
                : DomainWorkSource.Employer);
        if (source == DomainWorkSource.Integration && actor.Role != HoursActorRole.System)
        {
            return Results.Forbid();
        }

        var existingEvents = await journal.ReadEmployeeAsync(
            request.EmployeeId,
            context.RequestAborted);
        var snapshot = configuration.GetSnapshot(
            request.EmployeeId,
            request.NominalDate,
            ReadPublications(existingEvents));
        if (!existingEvents
            .Where(entry => entry.Type == HoursEventType.ConfigurationSnapshotRecorded)
            .Select(entry => entry.ReadPayload<ConfigurationSnapshot>())
            .Any(existing => existing.Id == snapshot.Id))
        {
            await journal.AppendAsync(
                HoursEvent.Create(
                    request.EmployeeId,
                    HoursEventType.ConfigurationSnapshotRecorded,
                    snapshot,
                    actor,
                    DateTimeOffset.UtcNow),
                context.RequestAborted);
        }

        var actorRef = new ActorRef(
            actor.Id,
            actor.DisplayName,
            actor.Role.ToString());
        var registration = await registrations.RecordAsync(
            new RecordWorkRegistrationCommand(
                new WorkDayKey(request.EmployeeId, request.NominalDate),
                source,
                (WorkRecordIntent)request.Intent,
                request.Intervals
                    .Select(interval => new WorkInterval(interval.Start, interval.End))
                    .ToImmutableArray(),
                request.CorrectsRegistrationId,
                request.Note,
                actorRef,
                snapshot.Id),
            context.RequestAborted);
        var shape = configuration.GetCalendar(
                request.EmployeeId,
                request.NominalDate,
                snapshot.CalendarVersion)
            .GetDayShape(request.NominalDate);
        var resolved = new Novolis.Hours.Domain.Calendars.WorkDayResolver().Resolve(registration, shape);
        var dimensions = new Novolis.Hours.Domain.Dimensions.DimensionEvaluator().Evaluate(
            resolved,
            configuration.GetDimensions(request.EmployeeId));
        var ledgerProjector = configuration.GetLedgerProjector();
        var originalTransaction = request.CorrectsRegistrationId is { } correctedId
            ? existingEvents
                .Where(entry => entry.Type == HoursEventType.LedgerTransactionRecorded)
                .Select(entry => entry.ReadPayload<Novolis.Hours.Domain.Ledger.LedgerTransaction>())
                .SingleOrDefault(transaction => transaction.SourceRegistrationId == correctedId)
            : null;
        if (originalTransaction is null)
        {
            await ledger.AppendTransactionAsync(
                registration,
                dimensions,
                ledgerProjector,
                context.RequestAborted);
        }
        else
        {
            await ledger.AppendCorrectionTransactionAsync(
                originalTransaction,
                registration,
                dimensions,
                ledgerProjector,
                context.RequestAborted);
        }
        HoursServerTelemetry.JournalAppends.Add(1);
        return Results.Created(
            $"/api/v2/employees/{Uri.EscapeDataString(request.EmployeeId)}/work-registrations",
            ToResponse(registration));
    }

    private static async Task<IResult> RecordDimensionAssignmentAsync(
        string employeeId,
        RecordDimensionAssignmentRequest request,
        HttpContext context,
        IHoursJournal journal,
        HoursUserDirectory users)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(employeeId, request.EmployeeId, StringComparison.Ordinal))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["employeeId"] = ["The route and request employee identifiers must match."],
            });
        }

        var actor = HoursServerPrincipalFactory.ToActor(context.User);
        if (!CanRecord(actor, employeeId, users))
        {
            return Results.Forbid();
        }

        if (request.Intervals.IsDefaultOrEmpty)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["intervals"] = ["A Dimension assignment requires at least one interval."],
            });
        }

        var assignment = new DimensionAssignment(
            Guid.CreateVersion7(),
            new WorkDayKey(employeeId, request.NominalDate),
            request.DimensionId,
            request.ValueId,
            request.Intervals
                .Select(interval => new WorkInterval(interval.Start, interval.End))
                .ToImmutableArray(),
            request.CorrectsAssignmentId,
            new ActorRef(actor.Id, actor.DisplayName, actor.Role.ToString()),
            DateTimeOffset.UtcNow);
        var entry = HoursEvent.Create(
            employeeId,
            HoursEventType.DimensionAssignmentRecorded,
            assignment,
            actor,
            assignment.RecordedAt);
        await journal.AppendAsync(entry, context.RequestAborted);
        return Results.Created(
            $"/api/v2/employees/{Uri.EscapeDataString(employeeId)}/dimension-assignments/{assignment.Id}",
            assignment);
    }

    private static async Task<IResult> PublishConfigurationAsync(
        string employeeId,
        PublishConfigurationRequest request,
        HttpContext context,
        HoursAcceptanceConfiguration configuration,
        IHoursJournal journal,
        HoursUserDirectory users)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = HoursServerPrincipalFactory.ToActor(context.User);
        if (!CanPublishConfiguration(actor, employeeId, users))
        {
            return Results.Forbid();
        }

        if (request.EffectiveFrom == default)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["effectiveFrom"] = ["An effective date is required."],
            });
        }

        var existing = await journal.ReadEmployeeAsync(employeeId, context.RequestAborted);
        var existingPublication = existing
            .Where(entry => entry.Type == HoursEventType.ConfigurationPublished)
            .Select(entry => entry.ReadPayload<ConfigurationPublication>())
            .Where(publication => publication.EffectiveFrom == request.EffectiveFrom)
            .OrderByDescending(publication => publication.Snapshot.Id.Value)
            .FirstOrDefault();
        var snapshot = existingPublication?.Snapshot ??
            configuration.CreatePublishedSnapshot(employeeId, request.EffectiveFrom);
        if (existingPublication is null)
        {
            await journal.AppendAsync(
                HoursEvent.Create(
                    employeeId,
                    HoursEventType.ConfigurationPublished,
                    new ConfigurationPublication(employeeId, request.EffectiveFrom, snapshot),
                    actor,
                    DateTimeOffset.UtcNow),
                context.RequestAborted);
        }

        if (!existing
            .Where(entry => entry.Type == HoursEventType.ConfigurationSnapshotRecorded)
            .Select(entry => entry.ReadPayload<ConfigurationSnapshot>())
            .Any(candidate => candidate.Id == snapshot.Id))
        {
            await journal.AppendAsync(
                HoursEvent.Create(
                    employeeId,
                    HoursEventType.ConfigurationSnapshotRecorded,
                    snapshot,
                    actor,
                    DateTimeOffset.UtcNow),
                context.RequestAborted);
        }

        return Results.Ok(new ConfigurationSnapshotResponse(
            snapshot.Id.Value,
            snapshot.CalendarVersion,
            snapshot.DimensionVersion,
            snapshot.ComplianceVersion,
            snapshot.WorkflowVersion,
            snapshot.LedgerVersion,
            snapshot.TimeZoneId));
    }

    private static async Task<IResult> ReadWorkRegistrationsAsync(
        string employeeId,
        HttpContext context,
        IHoursJournal journal,
        HoursUserDirectory users)
    {
        var actor = HoursServerPrincipalFactory.ToActor(context.User);
        if (!CanView(actor, employeeId, users))
        {
            return Results.Forbid();
        }

        var events = await journal.ReadEmployeeAsync(
            employeeId,
            context.RequestAborted);
        var registrations = events
            .Where(entry => entry.Type == HoursEventType.WorkRegistrationRecorded)
            .Select(entry => entry.ReadPayload<WorkRegistration>())
            .OrderBy(registration => registration.WorkDay.NominalDate)
            .ThenBy(registration => registration.RecordedAt)
            .Select(ToResponse)
            .ToArray();
        return Results.Ok(registrations);
    }

    private static async Task<IResult> ReadAuditAsync(
        string employeeId,
        HttpContext context,
        IHoursJournal journal,
        HoursUserDirectory users)
    {
        var actor = HoursServerPrincipalFactory.ToActor(context.User);
        if (!CanView(actor, employeeId, users))
        {
            return Results.Forbid();
        }

        var events = await journal.ReadEmployeeAsync(
            employeeId,
            context.RequestAborted);
        return Results.Ok(events.Select(entry => new HoursAuditEventResponse(
            entry.Id,
            entry.EmployeeId,
            entry.Type,
            entry.Actor.Id,
            entry.Actor.Role.ToString(),
            entry.OccurredAtUtc,
            GetPayloadReference(entry))));
    }

    private static async Task<IResult> ReadWorkDayAsync(
        string employeeId,
        DateOnly date,
        HttpContext context,
        HoursAcceptanceProjectionService projections,
        HoursUserDirectory users)
    {
        var actor = HoursServerPrincipalFactory.ToActor(context.User);
        if (!CanView(actor, employeeId, users))
        {
            return Results.Forbid();
        }

        return Results.Ok(await projections.GetWorkDayAsync(
            employeeId,
            date,
            context.RequestAborted));
    }

    private static async Task<IResult> ReadWorkDaysAsync(
        string employeeId,
        DateOnly from,
        DateOnly through,
        HttpContext context,
        HoursAcceptanceProjectionService projections,
        HoursUserDirectory users)
    {
        var actor = HoursServerPrincipalFactory.ToActor(context.User);
        if (!CanView(actor, employeeId, users))
        {
            return Results.Forbid();
        }

        if (from == default || through == default)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["range"] = ["Both from and through dates are required."],
            });
        }

        if (through < from)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["through"] = ["The through date must not precede the from date."],
            });
        }

        if (through.DayNumber - from.DayNumber + 1 > 31)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["through"] = ["A WorkDay range may cover at most 31 days."],
            });
        }

        return Results.Ok(await projections.GetWorkDaysAsync(
            employeeId,
            from,
            through,
            context.RequestAborted));
    }

    private static async Task<IResult> ReadReviewAsync(
        Guid periodId,
        HttpContext context,
        HoursAcceptanceProjectionService projections,
        HoursUserDirectory users)
    {
        var projection = await projections.GetReviewAsync(periodId, context.RequestAborted);
        var actor = HoursServerPrincipalFactory.ToActor(context.User);
        return CanView(actor, projection.EmployeeId, users)
            ? Results.Ok(projection)
            : Results.Forbid();
    }

    private static async Task<IResult> CreateReviewPeriodAsync(
        CreateReviewPeriodRequest request,
        HttpContext context,
        IHoursJournal journal,
        HoursUserDirectory users)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = HoursServerPrincipalFactory.ToActor(context.User);
        if (!CanView(actor, request.EmployeeId, users) ||
            actor.Role == HoursActorRole.Auditor)
        {
            return Results.Forbid();
        }

        var period = ReviewPeriod.Create(request.EmployeeId, request.From, request.Through);
        var entry = HoursEvent.Create(
            request.EmployeeId,
            HoursEventType.ReviewPeriodCreated,
            period,
            actor,
            DateTimeOffset.UtcNow);
        await journal.AppendAsync(entry, context.RequestAborted);
        return Results.Created($"/api/v2/reviews/{period.Id}", period);
    }

    private static async Task<IResult> RecordReviewActionAsync(
        Guid periodId,
        RecordReviewActionRequest request,
        HttpContext context,
        HoursAcceptanceProjectionService projections,
        IHoursJournal journal,
        HoursUserDirectory users)
    {
        ArgumentNullException.ThrowIfNull(request);
        var projection = await projections.GetReviewAsync(periodId, context.RequestAborted);
        var actor = HoursServerPrincipalFactory.ToActor(context.User);
        if (!Enum.TryParse<ReviewActionKind>(request.Kind, true, out var kind))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["kind"] = ["The review action kind is not recognised."],
            });
        }

        if (!CanRecordReview(actor, projection.EmployeeId, kind, users))
        {
            return Results.Forbid();
        }

        var currentHead = await projections.GetJournalHeadAsync(context.RequestAborted);
        if (request.ExpectedJournalHead.HasValue &&
            request.ExpectedJournalHead != currentHead)
        {
            return Results.Conflict(new
            {
                message = "The review state changed after it was opened. Reload before recording this action.",
                currentHead,
            });
        }

        var approvalLevel = request.ApprovalLevel;
        if (!approvalLevel.HasValue &&
            actor.Role == HoursActorRole.Manager)
        {
            approvalLevel = users.FindByEmployeeId(actor.Id)?.ApprovalLevel;
        }

        var action = ReviewAction.Create(
            periodId,
            kind,
            new ActorRef(actor.Id, actor.DisplayName, actor.Role.ToString()),
            approvalLevel,
            request.Comment,
            DateTimeOffset.UtcNow);
        var entry = HoursEvent.Create(
            projection.EmployeeId,
            HoursEventType.ReviewActionRecorded,
            action,
            actor,
            action.RecordedAt);
        await journal.AppendAsync(entry, context.RequestAborted);
        return Results.Created($"/api/v2/reviews/{periodId}", HoursAcceptanceProjectionService.ToResponse(action));
    }

    private static async Task<IResult> ReadHealthReportAsync(
        HttpContext context,
        HoursAcceptanceProjectionService projections,
        HoursUserDirectory users,
        string? teamId,
        string? employeeId)
    {
        var actor = HoursServerPrincipalFactory.ToActor(context.User);
        if (!CanReadReports(actor) ||
            !CanReadReportScope(actor, teamId, employeeId, users))
        {
            return Results.Forbid();
        }

        var report = await projections.GetHealthReportAsync(context.RequestAborted);
        return Results.Ok(FilterHealthReport(report, teamId, employeeId, users));
    }

    private static async Task<IResult> ReadBusinessPressureReportAsync(
        HttpContext context,
        HoursAcceptanceProjectionService projections,
        HoursUserDirectory users,
        string? teamId,
        string? employeeId)
    {
        var actor = HoursServerPrincipalFactory.ToActor(context.User);
        if (!CanReadReports(actor) ||
            !CanReadReportScope(actor, teamId, employeeId, users))
        {
            return Results.Forbid();
        }

        var report = await projections.GetBusinessPressureReportAsync(context.RequestAborted);
        return Results.Ok(FilterBusinessPressureReport(report, teamId, employeeId, users));
    }

    private static WorkRegistrationResponse ToResponse(WorkRegistration registration) =>
        new(
            registration.Id,
            registration.WorkDay.EmployeeId,
            registration.WorkDay.NominalDate,
            registration.Source.ToString(),
            (WorkRegistrationIntent)registration.Intent,
            registration.Intervals
                .Select(interval => new WorkIntervalRequest(interval.Start, interval.End))
                .ToImmutableArray(),
            registration.CorrectsRegistrationId,
            registration.Note,
            registration.RecordedBy.Id,
            registration.RecordedBy.Role,
            registration.RecordedAt,
            registration.ConfigurationSnapshotId.Value);

    private static string? GetPayloadReference(HoursEvent entry) =>
        entry.Type switch
        {
            HoursEventType.WorkRegistrationRecorded =>
                entry.ReadPayload<WorkRegistration>().Id.ToString("D"),
            HoursEventType.LedgerTransactionRecorded =>
                entry.ReadPayload<Novolis.Hours.Domain.Ledger.LedgerTransaction>().Id.ToString("D"),
            HoursEventType.ReviewPeriodCreated =>
                entry.ReadPayload<ReviewPeriod>().Id.ToString("D"),
            HoursEventType.ReviewActionRecorded =>
                entry.ReadPayload<ReviewAction>().Id.ToString("D"),
            HoursEventType.DimensionAssignmentRecorded =>
                entry.ReadPayload<DimensionAssignment>().Id.ToString("D"),
            HoursEventType.ConfigurationSnapshotRecorded =>
                entry.ReadPayload<ConfigurationSnapshot>().Id.Value.ToString("D"),
            HoursEventType.ConfigurationPublished =>
                entry.ReadPayload<ConfigurationPublication>().Snapshot.Id.Value.ToString("D"),
            _ => null,
        };

    private static ImmutableArray<ConfigurationPublication> ReadPublications(
        IEnumerable<HoursEvent> events) =>
        events
            .Where(entry => entry.Type == HoursEventType.ConfigurationPublished)
            .Select(entry => entry.ReadPayload<ConfigurationPublication>())
            .ToImmutableArray();

    private static bool CanView(
        HoursActor actor,
        string employeeId,
        HoursUserDirectory users)
    {
        if (actor.Role is HoursActorRole.Administrator or HoursActorRole.HumanResources or HoursActorRole.Higher or HoursActorRole.Auditor or HoursActorRole.System)
        {
            return true;
        }

        if (string.Equals(actor.Id, employeeId, StringComparison.Ordinal))
        {
            return true;
        }

        if (actor.Role != HoursActorRole.Manager)
        {
            return false;
        }

        var manager = users.FindByEmployeeId(actor.Id);
        var target = users.FindByEmployeeId(employeeId);
        if (manager is null || target is null)
        {
            return false;
        }

        return manager.ApprovalLevel >= 2
            ? string.Equals(manager.DivisionId, target.DivisionId, StringComparison.Ordinal)
            : string.Equals(manager.TeamId, target.TeamId, StringComparison.Ordinal);
    }

    private static bool CanRecord(
        HoursActor actor,
        string employeeId,
        HoursUserDirectory users,
        DomainWorkSource? requestedSource = null) =>
        actor.Role == HoursActorRole.System
            ? requestedSource is null or DomainWorkSource.Integration
            : actor.Role == HoursActorRole.Employee
                ? requestedSource is null or DomainWorkSource.Employee
                : requestedSource != DomainWorkSource.Integration &&
                  requestedSource != DomainWorkSource.System &&
                  actor.Role is not HoursActorRole.Auditor &&
                  CanView(actor, employeeId, users);

    private static bool CanPublishConfiguration(
        HoursActor actor,
        string employeeId,
        HoursUserDirectory users) =>
        actor.Role is HoursActorRole.Administrator or HoursActorRole.HumanResources or HoursActorRole.Higher &&
        CanView(actor, employeeId, users);

    private static bool CanRecordReview(
        HoursActor actor,
        string employeeId,
        ReviewActionKind kind,
        HoursUserDirectory users)
    {
        if (!CanView(actor, employeeId, users) ||
            actor.Role == HoursActorRole.Auditor)
        {
            return false;
        }

        return kind switch
        {
            ReviewActionKind.Submit or ReviewActionKind.Dispute =>
                actor.Role == HoursActorRole.Employee &&
                string.Equals(actor.Id, employeeId, StringComparison.Ordinal),
            ReviewActionKind.Acknowledge =>
                actor.Role == HoursActorRole.Employee &&
                string.Equals(actor.Id, employeeId, StringComparison.Ordinal) ||
                actor.Role is HoursActorRole.Manager or HoursActorRole.HumanResources or HoursActorRole.Higher or HoursActorRole.Administrator,
            ReviewActionKind.Approve =>
                actor.Role is HoursActorRole.Manager or HoursActorRole.HumanResources or HoursActorRole.Higher or HoursActorRole.Administrator,
            ReviewActionKind.Resolve =>
                actor.Role is HoursActorRole.HumanResources or HoursActorRole.Higher or HoursActorRole.Administrator,
            ReviewActionKind.Comment => true,
            _ => false,
        };
    }

    private static bool CanReadReports(HoursActor actor) =>
        actor.Role is HoursActorRole.Manager or HoursActorRole.HumanResources or HoursActorRole.Higher or HoursActorRole.Auditor or HoursActorRole.Administrator or HoursActorRole.System;

    private static bool CanReadReportScope(
        HoursActor actor,
        string? teamId,
        string? employeeId,
        HoursUserDirectory users)
    {
        if (employeeId is not null && !CanView(actor, employeeId, users))
        {
            return false;
        }

        if (teamId is null)
        {
            return true;
        }

        if (actor.Role is HoursActorRole.Administrator or HoursActorRole.HumanResources or HoursActorRole.Higher or HoursActorRole.Auditor or HoursActorRole.System)
        {
            return true;
        }

        if (actor.Role != HoursActorRole.Manager)
        {
            return false;
        }

        var manager = users.FindByEmployeeId(actor.Id);
        if (manager is null)
        {
            return false;
        }

        return manager.ApprovalLevel >= 2
            ? users.List().Any(user =>
                string.Equals(user.TeamId, teamId, StringComparison.Ordinal) &&
                string.Equals(user.DivisionId, manager.DivisionId, StringComparison.Ordinal))
            : string.Equals(manager.TeamId, teamId, StringComparison.Ordinal);
    }

    private static HealthConcernsReportResponse FilterHealthReport(
        HealthConcernsReportResponse report,
        string? teamId,
        string? employeeId,
        HoursUserDirectory users)
    {
        if (teamId is null && employeeId is null)
        {
            return report;
        }

        return report with
        {
            Rows = report.Rows
                .Select(row => row with
                {
                    WorkDays = row.WorkDays
                        .Where(key => MatchesReportScope(key, teamId, employeeId, users))
                        .ToImmutableArray(),
                })
                .Where(row => !row.WorkDays.IsEmpty)
                .ToImmutableArray(),
        };
    }

    private static BusinessPressureReportResponse FilterBusinessPressureReport(
        BusinessPressureReportResponse report,
        string? teamId,
        string? employeeId,
        HoursUserDirectory users)
    {
        if (teamId is null && employeeId is null)
        {
            return report;
        }

        return report with
        {
            Rows = report.Rows
                .Select(row => row with
                {
                    WorkDays = row.WorkDays
                        .Where(key => MatchesReportScope(key, teamId, employeeId, users))
                        .ToImmutableArray(),
                })
                .Where(row => !row.WorkDays.IsEmpty)
                .ToImmutableArray(),
        };
    }

    private static bool MatchesReportScope(
        string key,
        string? teamId,
        string? employeeId,
        HoursUserDirectory users)
    {
        var separator = key.IndexOf(':');
        var candidateEmployeeId = separator < 0 ? key : key[..separator];
        var candidate = users.FindByEmployeeId(candidateEmployeeId);
        return candidate is not null &&
            (employeeId is null || string.Equals(candidateEmployeeId, employeeId, StringComparison.Ordinal)) &&
            (teamId is null || string.Equals(candidate.TeamId, teamId, StringComparison.Ordinal));
    }
}
