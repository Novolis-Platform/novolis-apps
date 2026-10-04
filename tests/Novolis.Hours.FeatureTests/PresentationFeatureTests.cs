using System.Collections.Immutable;
using Novolis.Hours.Client.Presentation;
using Novolis.Hours.Contracts;

namespace Novolis.Hours.FeatureTests;

public sealed class PresentationFeatureTests
{
    [Test]
    public async Task Role_owns_the_home_surface()
    {
        await Assert.That(HoursRoleSurface.Home(HoursClientRole.System)).IsEqualTo(HoursSurfaceKind.Backoffice);
        await Assert.That(HoursRoleSurface.ShowsTimesheet(HoursClientRole.System)).IsFalse();
        await Assert.That(HoursRoleSurface.HomePath(HoursClientRole.System)).IsEqualTo("/");
        await Assert.That(HoursRoleSurface.Home(HoursClientRole.Administrator)).IsEqualTo(HoursSurfaceKind.Workplace);
        await Assert.That(HoursRoleSurface.ShowsTimesheet(HoursClientRole.Administrator)).IsFalse();
        await Assert.That(HoursRoleSurface.Home(HoursClientRole.HumanResources)).IsEqualTo(HoursSurfaceKind.Reviews);
        await Assert.That(HoursRoleSurface.HomePath(HoursClientRole.HumanResources)).IsEqualTo("/reviews");
        await Assert.That(HoursRoleSurface.Home(HoursClientRole.Auditor)).IsEqualTo(HoursSurfaceKind.Audit);
        await Assert.That(HoursRoleSurface.Home(HoursClientRole.Employee)).IsEqualTo(HoursSurfaceKind.Week);
        await Assert.That(HoursRoleSurface.ShowsTimesheet(HoursClientRole.Employee)).IsTrue();
        await Assert.That(HoursRoleSurface.ShowsTimesheet(HoursClientRole.Manager)).IsFalse();
        await Assert.That(HoursRoleSurface.Home(HoursClientRole.Manager)).IsEqualTo(HoursSurfaceKind.Reviews);
        await Assert.That(HoursRoleSurface.HomePath(HoursClientRole.Administrator)).IsEqualTo("/workplace");
    }

    [Test]
    public async Task Geometry_snaps_and_hit_tests_actual_handles()
    {
        await Assert.That(DayStripGeometry.FromRatio(DayStripGeometry.ToRatio(new TimeOnly(8, 2))))
            .IsEqualTo(new TimeOnly(8, 0));
        var actual = new List<(TimeOnly Start, TimeOnly End)>
        {
            (new TimeOnly(8, 0), new TimeOnly(11, 30)),
        };
        var start = DayStripGeometry.HitTest(DayStripGeometry.ToRatio(new TimeOnly(8, 0)), actual);
        var body = DayStripGeometry.HitTest(DayStripGeometry.ToRatio(new TimeOnly(9, 30)), actual);
        var empty = DayStripGeometry.HitTest(DayStripGeometry.ToRatio(new TimeOnly(14, 0)), actual);
        await Assert.That(start.Kind).IsEqualTo(DayStripHitKind.StartHandle);
        await Assert.That(body.Kind).IsEqualTo(DayStripHitKind.Body);
        await Assert.That(empty.Kind).IsEqualTo(DayStripHitKind.Empty);
    }

    [Test]
    public async Task Paint_cannot_create_hours_outside_actual()
    {
        var studio = new DayStudioModel(SampleDay(registered: true));
        await Assert.That(studio.TryClipPaintToActual(new TimeOnly(18, 0), new TimeOnly(19, 0), out _, out _))
            .IsFalse();
        await Assert.That(studio.TryClipPaintToActual(new TimeOnly(10, 0), new TimeOnly(12, 0), out var start, out var end))
            .IsTrue();
        await Assert.That(start).IsEqualTo(new TimeOnly(10, 0));
        await Assert.That(end).IsEqualTo(new TimeOnly(11, 30));
    }

    [Test]
    public async Task Layer_rail_keeps_silence_empty_and_lights_outside_envelope()
    {
        var studio = new DayStudioModel(SampleDay(registered: true));
        studio.BeginDrag(DayStripGeometry.ToRatio(new TimeOnly(18, 0)), paint: false);
        studio.MoveDrag(DayStripGeometry.ToRatio(new TimeOnly(19, 0)));
        studio.EndDrag();
        var rail = studio.LayerRail();
        await Assert.That(rail.Single(row => row.Kind == "Employment").Said).IsEqualTo(string.Empty);
        await Assert.That(rail.Single(row => row.Kind == "Organisation").Speaking).IsTrue();
        await Assert.That(studio.HasOutsideEnvelope()).IsTrue();
    }

    [Test]
    public async Task Week_tiles_mark_holiday_and_today()
    {
        var today = new DateOnly(2026, 10, 1);
        var week = new WeekStudioModel(
            [
                SampleDay(today, working: true, holiday: false),
                SampleDay(new DateOnly(2026, 10, 4), working: false, holiday: false),
                SampleDay(new DateOnly(2026, 9, 30), working: false, holiday: true),
            ],
            today);
        await Assert.That(week.WeekStart).IsEqualTo(new DateOnly(2026, 9, 28));
        await Assert.That(week.Tiles.Single(tile => tile.Date == today).IsToday).IsTrue();
        await Assert.That(week.Tiles.Single(tile => tile.Date == new DateOnly(2026, 10, 4)).Chip)
            .IsEqualTo("Closed");
        await Assert.That(week.Tiles.Single(tile => tile.Date == new DateOnly(2026, 9, 30)).Chip)
            .IsEqualTo("Holiday");
        var sunday = new WeekStudioModel(
            [SampleDay(new DateOnly(2026, 10, 4), working: true, holiday: false)],
            today);
        await Assert.That(sunday.Tiles.Single().Chip).IsEqualTo("Not recorded");
    }

    [Test]
    public async Task Norwegian_office_studio_keeps_flex_paint_and_worked_as_scheduled()
    {
        var studio = new DayStudioModel(SampleDay(registered: true));
        var week = new WeekStudioModel(
            [
                SampleDay(new DateOnly(2026, 10, 1), working: true, holiday: false, registered: true),
                SampleDay(new DateOnly(2026, 10, 3), working: false, holiday: false),
            ],
            new DateOnly(2026, 10, 1));
        var review = new ReviewMonthModel(
            SampleReview(
                "cascading-approval",
                allowsDispute: true,
                attendance: false,
                ("employee-submit", "Submit"),
                ("manager-level-1", "Approve"),
                ("hr-final", "Approve")),
            [SampleDay(registered: true)]);

        await Assert.That(studio.CommitLabel).IsEqualTo("I worked as planned");
        await Assert.That(studio.AllowsPaint).IsTrue();
        await Assert.That(studio.WorkplaceName).IsEqualTo("Nordvik");
        await Assert.That(week.Tiles.Single(tile => tile.Date == new DateOnly(2026, 10, 1)).Chip)
            .IsEqualTo("As planned");
        await Assert.That(week.Tiles.Single(tile => tile.Date == new DateOnly(2026, 10, 3)).Chip)
            .IsEqualTo("Closed");
        await Assert.That(review.EmployeePrimaryLabel).IsEqualTo("Hand month to HR");
        await Assert.That(review.AllowsDispute).IsTrue();
    }

    [Test]
    public async Task Game_studio_is_attendance_only_without_flex_or_dispute()
    {
        var studio = new DayStudioModel(SampleGameDay(registered: false));
        studio.BeginDrag(DayStripGeometry.ToRatio(new TimeOnly(10, 0)), paint: false);
        await Assert.That(studio.IsDragging).IsFalse();
        var week = new WeekStudioModel(
            [
                SampleGameDay(new DateOnly(2026, 3, 9), working: true, registered: false),
                SampleGameDay(new DateOnly(2026, 3, 14), working: true, registered: true),
                SampleGameDay(new DateOnly(2026, 3, 15), working: false, registered: false),
            ],
            new DateOnly(2026, 3, 9));
        var review = new ReviewMonthModel(
            SampleReview(
                "attendance-hr",
                allowsDispute: false,
                attendance: true,
                ("employee-confirm", "Acknowledge"),
                ("hr-attendance", "Approve")),
            [SampleGameDay(registered: true)]);

        await Assert.That(studio.CommitLabel).IsEqualTo("I worked as planned");
        await Assert.That(studio.AllowsPaint).IsFalse();
        await Assert.That(studio.WorkplaceName).IsEqualTo("Game");
        await Assert.That(week.Tiles.Single(tile => tile.Date == new DateOnly(2026, 3, 9)).Chip)
            .IsEqualTo("Not recorded");
        await Assert.That(week.Tiles.Single(tile => tile.Date == new DateOnly(2026, 3, 14)).Chip)
            .IsEqualTo("As planned");
        await Assert.That(week.Tiles.Single(tile => tile.Date == new DateOnly(2026, 3, 15)).Chip)
            .IsEqualTo("Closed");
        await Assert.That(review.EmployeePrimaryLabel).IsEqualTo("Hand month to HR");
        await Assert.That(review.AllowsDispute).IsFalse();
        await Assert.That(review.HasSubmitStage).IsFalse();
    }

    [Test]
    public async Task Worker_copy_names_gaps_changed_clocks_and_closed_days()
    {
        var late = SampleGameDay(
            new DateOnly(2026, 10, 1),
            working: true,
            registered: true,
            intent: WorkRegistrationIntent.ManualRegistration);
        var forgotten = SampleGameDay(new DateOnly(2026, 10, 2), working: true, registered: false);
        var shut = SampleGameDay(new DateOnly(2026, 10, 4), working: false, registered: false);
        var planned = SampleGameDay(new DateOnly(2026, 9, 28), working: true, registered: true);
        var week = new WeekStudioModel([planned, late, forgotten, shut], new DateOnly(2026, 10, 1));
        var review = new ReviewMonthModel(
            SampleReview(
                "attendance-hr",
                allowsDispute: false,
                attendance: true,
                ("employee-confirm", "Acknowledge"),
                ("hr-attendance", "Approve")),
            [planned, late, forgotten, shut]);

        await Assert.That(WeekStudioModel.ChipFor(late)).IsEqualTo("Changed");
        await Assert.That(WeekStudioModel.DetailFor(late)).IsEqualTo("08:00–11:30 · 3:30");
        await Assert.That(WeekStudioModel.ChipFor(forgotten)).IsEqualTo("Not recorded");
        await Assert.That(WeekStudioModel.DetailFor(forgotten)).IsEqualTo("Still to record");
        await Assert.That(WeekStudioModel.ChipFor(shut)).IsEqualTo("Closed");
        await Assert.That(WeekStudioModel.DetailFor(shut)).IsEqualTo("Shop shut");
        await Assert.That(week.GapLabel).IsEqualTo("1 working day is not recorded.");
        await Assert.That(week.ChangedLabel).IsEqualTo("1 day was changed from the usual hours.");
        await Assert.That(review.GapLabel).IsEqualTo("1 working day is not recorded.");
        await Assert.That(review.ChangedLabel).IsEqualTo("1 day was changed from the usual hours.");
        await Assert.That(ReviewMonthModel.StageActorLabel(review.Review.Stages[0])).IsEqualTo("You");
        await Assert.That(ReviewMonthModel.StageActorLabel(review.Review.Stages[1])).IsEqualTo("HR");
        await Assert.That(new DayStudioModel(forgotten).RecordedHoursLabel).IsEqualTo("Nothing recorded yet.");
    }

    [Test]
    public async Task Customer_open_requires_location_name_and_administrator()
    {
        var locations = new HoursLocationResponse[]
        {
            new("united-kingdom", "United Kingdom", "GB", "Europe/London", "English public holidays."),
            new("norway", "Norway", "NO", "Europe/Oslo", "Norwegian public holidays."),
        };
        var open = new CustomerOpenModel(locations);
        await Assert.That(open.CanOpen).IsFalse();
        open.DisplayName = "Game High Street";
        open.SelectedLocationId = "united-kingdom";
        open.AdminDisplayName = "Pat Shop Admin";
        open.AdminLogin = "pat";
        await Assert.That(open.CanOpen).IsTrue();
        await Assert.That(open.ResolvedOrganisationId).IsEqualTo("game-high-street");
        await Assert.That(open.DivisionId).IsEqualTo("division-uk");
        await Assert.That(open.SelectedLocation!.TimeZoneId).IsEqualTo("Europe/London");
        open.SelectedLocationId = "norway";
        await Assert.That(open.DivisionId).IsEqualTo("division-north");
    }

    [Test]
    public async Task Month_is_iso_weeks_of_seven_days()
    {
        var today = new DateOnly(2026, 10, 4);
        var month = new HoursMonthModel(
            2026,
            10,
            [
                SampleGameDay(new DateOnly(2026, 10, 1), working: true, registered: false),
                SampleGameDay(new DateOnly(2026, 10, 4), working: false, registered: false),
            ],
            today);

        await Assert.That(month.Title).IsEqualTo("October 2026");
        await Assert.That(month.Weeks.Length).IsGreaterThanOrEqualTo(4);
        await Assert.That(month.Weeks[0].WeekNumber).IsEqualTo(40);
        await Assert.That(month.Weeks[0].Days.Length).IsEqualTo(7);
        await Assert.That(month.Weeks[0].Monday).IsEqualTo(new DateOnly(2026, 9, 28));
        await Assert.That(month.Weeks[0].NumberLabel).IsEqualTo("W40");
        await Assert.That(month.Weeks[0].Days.Count(day => day.InMonth)).IsEqualTo(4);
        await Assert.That(HoursClock.FormatMonth(new DateOnly(2026, 10, 1))).IsEqualTo("October 2026");
    }

    [Test]
    public async Task Mixed_roles_keep_a_week_and_workplace()
    {
        var managerEmployeeAdmin = new[]
        {
            HoursClientRole.Manager,
            HoursClientRole.Employee,
            HoursClientRole.Administrator,
        };
        var hrAdmin = new[] { HoursClientRole.HumanResources, HoursClientRole.Administrator };

        await Assert.That(HoursRoleSurface.ShowsTimesheet(managerEmployeeAdmin)).IsTrue();
        await Assert.That(HoursRoleSurface.ShowsWorkplace(managerEmployeeAdmin)).IsTrue();
        await Assert.That(HoursRoleSurface.ShowsReviews(managerEmployeeAdmin)).IsTrue();
        await Assert.That(HoursRoleSurface.Home(managerEmployeeAdmin)).IsEqualTo(HoursSurfaceKind.Week);
        await Assert.That(HoursRoleSurface.HomePath(managerEmployeeAdmin)).IsEqualTo("/");
        await Assert.That(HoursRoleSurface.ShowsTimesheet(hrAdmin)).IsFalse();
        await Assert.That(HoursRoleSurface.Home(hrAdmin)).IsEqualTo(HoursSurfaceKind.Workplace);
        await Assert.That(HoursRoleSurface.HomePath(hrAdmin)).IsEqualTo("/workplace");
        await Assert.That(HoursClientRoleSet.Label(hrAdmin)).IsEqualTo("HR · Admin");
    }

    [Test]
    public async Task Human_clocks_drop_seconds()
    {
        await Assert.That(HoursClock.Format(TimeSpan.FromHours(7.5))).IsEqualTo("7:30");
        await Assert.That(HoursClock.Format(TimeSpan.FromHours(4.25), signed: true)).IsEqualTo("+4:15");
        await Assert.That(HoursClock.FormatDate(new DateOnly(2026, 10, 1)))
            .Contains("Thursday");
    }

    private static ReviewProjectionResponse SampleReview(
        string policyId,
        bool allowsDispute,
        bool attendance,
        params (string Id, string Action)[] stages) =>
        new(
            Guid.CreateVersion7(),
            attendance ? "jamie" : "ada",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31),
            policyId,
            "Open",
            false,
            [],
            stages.Select(stage => new ReviewStageResponse(
                stage.Id,
                stage.Action,
                stage.Action == "Approve" ? "HumanResources" : "Employee",
                null,
                new DateOnly(2026, 10, 5),
                false,
                false,
                null)).ToImmutableArray(),
            [],
            [],
            null,
            allowsDispute,
            attendance);

    private static WorkDayResponse SampleGameDay(
        DateOnly? date = null,
        bool working = true,
        bool registered = false,
        WorkRegistrationIntent intent = WorkRegistrationIntent.WorkedAsScheduled) =>
        SampleDay(
            date,
            working,
            holiday: false,
            registered,
            "jamie",
            "game-retail",
            "Game",
            "Europe/London",
            allowsFlex: false,
            allowsDispute: false,
            attendance: true,
            intent);

    private static WorkDayResponse SampleDay(
        DateOnly? date = null,
        bool working = true,
        bool holiday = false,
        bool registered = false,
        string employeeId = "ada",
        string organisationId = "nordvik-office",
        string organisationName = "Nordvik",
        string timeZoneId = "Europe/Oslo",
        bool allowsFlex = true,
        bool allowsDispute = true,
        bool attendance = false,
        WorkRegistrationIntent intent = WorkRegistrationIntent.WorkedAsScheduled) =>
        new(
            employeeId,
            organisationId,
            date ?? new DateOnly(2026, 10, 1),
            working,
            TimeSpan.FromHours(7.5),
            TimeSpan.Zero,
            "07:00–17:00",
            ["09:00–15:00"],
            ["08:00–11:30", "12:30–16:30"],
            holiday ? ["PublicHoliday: Første mai"] : [],
            [
                new AppliedDayRuleResponse(
                    "no.national.weekdays",
                    "no.national",
                    "v1",
                    100,
                    "WorkingDayRule",
                    "Working day = True",
                    "Generated",
                    "National",
                    null,
                    null,
                    null,
                    null,
                    null),
                new AppliedDayRuleResponse(
                    "nordvik-office.organisation.shape",
                    "nordvik-office.organisation",
                    "v1",
                    200,
                    "WorkEnvelopeRule",
                    "Envelope = 07:00–17:00",
                    "Manual",
                    "Organisation",
                    null,
                    null,
                    null,
                    null,
                    null),
            ],
            registered
                ? new WorkRegistrationResponse(
                    Guid.CreateVersion7(),
                    employeeId,
                    date ?? new DateOnly(2026, 10, 1),
                    "Employee",
                    intent,
                    [],
                    null,
                    null,
                    employeeId,
                    "Employee",
                    DateTimeOffset.UtcNow,
                    Guid.CreateVersion7())
                : null,
            registered
                ? [
                    new WorkIntervalRequest(
                        new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2026, 10, 1, 11, 30, 0, TimeSpan.Zero)),
                ]
                : [],
            registered ? TimeSpan.FromHours(3.5) : TimeSpan.Zero,
            TimeSpan.Zero,
            TimeSpan.Zero,
            [],
            [],
            [],
            [],
            [],
            new ConfigurationSnapshotResponse(
                Guid.CreateVersion7(),
                "v1",
                "v1",
                "v1",
                "v1",
                "v1",
                timeZoneId),
            new LocalTimeRangeDto(new TimeOnly(7, 0), new TimeOnly(17, 0)),
            [new LocalTimeRangeDto(new TimeOnly(9, 0), new TimeOnly(15, 0))],
            [
                new LocalTimeRangeDto(new TimeOnly(8, 0), new TimeOnly(11, 30)),
                new LocalTimeRangeDto(new TimeOnly(12, 30), new TimeOnly(16, 30)),
            ],
            [
                new DimensionBrushResponse(
                    "customer",
                    "Customer",
                    [new DimensionBrushValueResponse("acme", "ACME")]),
            ],
            allowsFlex,
            allowsDispute,
            attendance,
            organisationName);
}
