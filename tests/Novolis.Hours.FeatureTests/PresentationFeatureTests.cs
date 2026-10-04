using System.Collections.Immutable;
using Novolis.Hours.Client.Presentation;
using Novolis.Hours.Contracts;

namespace Novolis.Hours.FeatureTests;

public sealed class PresentationFeatureTests
{
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
            .IsEqualTo("Weekend");
        await Assert.That(week.Tiles.Single(tile => tile.Date == new DateOnly(2026, 9, 30)).Chip)
            .IsEqualTo("Holiday");
        var sunday = new WeekStudioModel(
            [SampleDay(new DateOnly(2026, 10, 4), working: true, holiday: false)],
            today);
        await Assert.That(sunday.Tiles.Single().Chip).IsEqualTo("Open");
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

        await Assert.That(studio.CommitLabel).IsEqualTo("Worked as scheduled");
        await Assert.That(studio.AllowsPaint).IsTrue();
        await Assert.That(studio.WorkplaceName).IsEqualTo("Nordvik");
        await Assert.That(week.Tiles.Single(tile => tile.Date == new DateOnly(2026, 10, 1)).Chip)
            .IsEqualTo("Recorded");
        await Assert.That(week.Tiles.Single(tile => tile.Date == new DateOnly(2026, 10, 3)).Chip)
            .IsEqualTo("Weekend");
        await Assert.That(review.EmployeePrimaryLabel).IsEqualTo("Submit");
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

        await Assert.That(studio.CommitLabel).IsEqualTo("I was here");
        await Assert.That(studio.AllowsPaint).IsFalse();
        await Assert.That(studio.WorkplaceName).IsEqualTo("Game");
        await Assert.That(week.Tiles.Single(tile => tile.Date == new DateOnly(2026, 3, 9)).Chip)
            .IsEqualTo("Confirm");
        await Assert.That(week.Tiles.Single(tile => tile.Date == new DateOnly(2026, 3, 14)).Chip)
            .IsEqualTo("Here");
        await Assert.That(week.Tiles.Single(tile => tile.Date == new DateOnly(2026, 3, 15)).Chip)
            .IsEqualTo("Weekend");
        await Assert.That(review.EmployeePrimaryLabel).IsEqualTo("Confirm attendance");
        await Assert.That(review.AllowsDispute).IsFalse();
        await Assert.That(review.HasSubmitStage).IsFalse();
    }

    [Test]
    public async Task Workplace_setup_preview_matches_Game_and_Nordvik_commit_words()
    {
        var game = new HoursWorkplaceTemplateResponse(
            "game-retail",
            "Game shop",
            "Attendance",
            new HoursCustomerResponse(
                "game-retail",
                "Game",
                "GB",
                "Europe/London",
                "england.retail.rigid",
                "attendance-hr",
                TimeSpan.FromHours(8),
                new LocalTimeRangeDto(new TimeOnly(9, 0), new TimeOnly(18, 0)),
                new LocalTimeRangeDto(new TimeOnly(9, 0), new TimeOnly(18, 0)),
                true,
                false,
                false,
                true,
                true));
        var nordvik = game with
        {
            Id = "nordvik-office",
            Title = "Nordvik office",
            Prototype = game.Prototype with
            {
                Id = "nordvik-office",
                DisplayName = "Nordvik",
                AttendanceConfirmationOnly = false,
                AllowsFlex = true,
                AllowsDispute = true,
            },
        };
        var setup = new WorkplaceSetupModel([game, nordvik]);
        setup.SelectedTemplateId = "game-retail";
        await Assert.That(setup.CommitPreview).IsEqualTo("I was here");
        await Assert.That(setup.PeopleHint).Contains("HR");
        setup.SelectedTemplateId = "nordvik-office";
        await Assert.That(setup.CommitPreview).IsEqualTo("Worked as scheduled");
        await Assert.That(setup.PeopleHint).Contains("manager");
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
        bool registered = false) =>
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
            attendance: true);

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
        bool attendance = false) =>
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
                    WorkRegistrationIntent.WorkedAsScheduled,
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
