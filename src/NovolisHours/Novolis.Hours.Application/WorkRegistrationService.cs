using Novolis.Hours.Domain;
using Novolis.Hours.Domain.Work;

namespace Novolis.Hours.Application;

/// <summary>Application boundary for appending work assertions without deriving policy meaning.</summary>
public sealed class WorkRegistrationService
{
    private readonly IHoursJournal journal;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes the service over an append-only journal.</summary>
    public WorkRegistrationService(IHoursJournal journal, TimeProvider? timeProvider = null)
    {
        this.journal = journal ?? throw new ArgumentNullException(nameof(journal));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Records one work assertion and preserves its configuration snapshot.</summary>
    public async ValueTask<WorkRegistration> RecordAsync(
        RecordWorkRegistrationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var recordedAt = command.RecordedAt ?? timeProvider.GetUtcNow();
        var registration = new WorkRegistration(
            Guid.CreateVersion7(),
            command.WorkDay,
            command.Source,
            command.Intent,
            command.Intervals,
            command.CorrectsRegistrationId,
            command.Note,
            command.RecordedBy,
            recordedAt,
            command.ConfigurationSnapshotId);

        var entry = HoursEvent.Create(
            registration.WorkDay.EmployeeId,
            HoursEventType.WorkRegistrationRecorded,
            registration,
            registration.RecordedBy.ToHoursActor(),
            registration.RecordedAt);
        await journal.AppendAsync(entry, cancellationToken).ConfigureAwait(false);
        return registration;
    }
}
