using Novolis.Hours.Client;
using Novolis.Hours.Client.Presentation;
using Novolis.Hours.Contracts;

namespace Novolis.Hours.Client.Cli;

/// <summary>Signed-in Hours API session for the employee CLI.</summary>
internal sealed class HoursCliSession : IDisposable
{
    /// <summary>Persisted host facts.</summary>
    public HoursSessionModel Model { get; } = HoursSessionPersistence.Load();

    /// <summary>Authenticated client, when signed in.</summary>
    public HoursApiClient? Api { get; private set; }

    /// <summary>Signed-in person.</summary>
    public HoursClientUser? User { get; private set; }

    /// <summary>Signs in and remembers the service URL.</summary>
    public async Task SignInAsync(string login, string password, CancellationToken cancellationToken)
    {
        Api?.Dispose();
        Api = HoursApiClient.Connect(new Uri(Model.ServiceUrl, UriKind.Absolute));
        User = await Api.SignInAsync(login, password, cancellationToken);
        Model.EmployeeId = User.EmployeeId;
        Model.DisplayName = User.DisplayName;
        HoursSessionPersistence.Save(Model);
    }

    /// <summary>Loads the current week for the signed-in person.</summary>
    public async Task<WeekStudioModel> LoadWeekAsync(DateOnly? weekStart, CancellationToken cancellationToken)
    {
        EnsureSignedIn();
        var start = weekStart ?? WeekStudioModel.MondayOnOrBefore(DateOnly.FromDateTime(DateTime.Today));
        var days = await Api!.GetWorkDaysAsync(User!.EmployeeId, start, start.AddDays(6), cancellationToken);
        if (days.Count > 0)
        {
            Model.OrganisationId = days[0].OrganisationId;
            Model.TimeZoneId = days[0].Configuration.TimeZoneId;
            HoursSessionPersistence.Save(Model);
        }

        return new WeekStudioModel(days, DateOnly.FromDateTime(DateTime.Today));
    }

    /// <summary>Loads one day studio.</summary>
    public async Task<DayStudioModel> LoadDayAsync(DateOnly date, CancellationToken cancellationToken)
    {
        EnsureSignedIn();
        var day = await Api!.GetWorkDayAsync(User!.EmployeeId, date, cancellationToken);
        Model.OrganisationId = day.OrganisationId;
        Model.TimeZoneId = day.Configuration.TimeZoneId;
        HoursSessionPersistence.Save(Model);
        return new DayStudioModel(day);
    }

    /// <summary>Records the scheduled-work assertion.</summary>
    public async Task RecordScheduledAsync(DateOnly date, CancellationToken cancellationToken)
    {
        EnsureSignedIn();
        await Api!.RecordWorkRegistrationAsync(
            new RecordWorkRegistrationRequest(
                User!.EmployeeId,
                date,
                WorkRegistrationIntent.WorkedAsScheduled,
                [],
                null,
                "Worked as planned."),
            cancellationToken);
    }

    /// <summary>Paints a clipped stroke onto already recorded actual work.</summary>
    public async Task PaintAsync(
        DateOnly date,
        TimeOnly from,
        TimeOnly to,
        string dimensionId,
        string valueId,
        CancellationToken cancellationToken)
    {
        var studio = await LoadDayAsync(date, cancellationToken);
        if (!studio.TryClipPaintToActual(from, to, out var start, out var end))
        {
            throw new InvalidOperationException("Paint cannot create hours outside actual work.");
        }

        await Api!.RecordDimensionAssignmentAsync(
            User!.EmployeeId,
            new RecordDimensionAssignmentRequest(
                User.EmployeeId,
                date,
                dimensionId,
                valueId,
                [
                    new WorkIntervalRequest(
                        HoursClock.ToNominalTimestamp(date, start, studio.Day.Configuration.TimeZoneId),
                        HoursClock.ToNominalTimestamp(date, end, studio.Day.Configuration.TimeZoneId)),
                ],
                null),
            cancellationToken);
    }

    /// <summary>Loads a month of days for review chrome.</summary>
    public async Task<IReadOnlyList<WorkDayResponse>> LoadMonthAsync(
        DateOnly month,
        CancellationToken cancellationToken)
    {
        EnsureSignedIn();
        var from = new DateOnly(month.Year, month.Month, 1);
        var through = from.AddMonths(1).AddDays(-1);
        return await Api!.GetWorkDaysAsync(User!.EmployeeId, from, through, cancellationToken);
    }

    /// <summary>Reads organisation pressure.</summary>
    public Task<BusinessPressureReportResponse> LoadPressureAsync(CancellationToken cancellationToken)
    {
        EnsureSignedIn();
        return Api!.GetBusinessPressureReportAsync(cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Api?.Dispose();
        Api = null;
        User = null;
    }

    private void EnsureSignedIn()
    {
        if (Api is null || User is null)
        {
            throw new InvalidOperationException("Sign in before talking to Hours.");
        }
    }
}
