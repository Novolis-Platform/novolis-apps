using System.Collections.Immutable;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Novolis.Hours.Application;
using Novolis.Hours.Contracts;
using Novolis.Hours.Domain;
using Novolis.Hours.Domain.Configuration;
using Novolis.Hours.Domain.Work;
using Novolis.Hours.Storage;

namespace Novolis.Hours.Server;

/// <summary>Maps the immutable v2 work-registration and audit API.</summary>
public static class HoursRewriteEndpointMappings
{
    /// <summary>Adds secured v2 work and audit routes without replacing legacy routes.</summary>
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v2").RequireAuthorization();
        api.MapPost("/work-registrations", RecordWorkRegistrationAsync);
        api.MapGet(
            "/employees/{employeeId}/work-registrations",
            ReadWorkRegistrationsAsync);
        api.MapGet(
            "/employees/{employeeId}/audit",
            ReadAuditAsync);
    }

    private static async Task<IResult> RecordWorkRegistrationAsync(
        RecordWorkRegistrationRequest request,
        HttpContext context,
        WorkRegistrationService registrations)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = HoursPrincipalFactory.ToActor(context.User);
        if (!CanRecord(actor, request.EmployeeId))
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

        var actorRef = new ActorRef(
            actor.Id,
            actor.DisplayName,
            actor.Role.ToString());
        var registration = await registrations.RecordAsync(
            new RecordWorkRegistrationCommand(
                new WorkDayKey(request.EmployeeId, request.NominalDate),
                actor.Role == HoursActorRole.Employee
                    ? WorkRecordSource.Employee
                    : WorkRecordSource.Employer,
                (WorkRecordIntent)request.Intent,
                request.Intervals
                    .Select(interval => new WorkInterval(interval.Start, interval.End))
                    .ToImmutableArray(),
                request.CorrectsRegistrationId,
                request.Note,
                actorRef,
                ConfigurationSnapshotId.New()),
            context.RequestAborted);
        HoursTelemetry.JournalAppends.Add(1);
        return Results.Created(
            $"/api/v2/employees/{Uri.EscapeDataString(request.EmployeeId)}/work-registrations",
            ToResponse(registration));
    }

    private static async Task<IResult> ReadWorkRegistrationsAsync(
        string employeeId,
        HttpContext context,
        IHoursJournal journal)
    {
        var actor = HoursPrincipalFactory.ToActor(context.User);
        if (!CanView(actor, employeeId))
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
        IHoursJournal journal)
    {
        var actor = HoursPrincipalFactory.ToActor(context.User);
        if (!CanView(actor, employeeId))
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
            entry.OccurredAtUtc)));
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

    private static bool CanView(HoursActor actor, string employeeId) =>
        actor.Role != HoursActorRole.Employee ||
        string.Equals(actor.Id, employeeId, StringComparison.Ordinal);

    private static bool CanRecord(HoursActor actor, string employeeId) =>
        actor.Role != HoursActorRole.Employee ||
        string.Equals(actor.Id, employeeId, StringComparison.Ordinal);
}
