using System.Net;
using Novolis.Hours.Client;
using Novolis.Hours.Contracts;
using Novolis.Hours.Domain;

namespace Novolis.Hours.FeatureTests;

/// <summary>HTTP acceptance scenarios for the named multi-user Hours composition.</summary>
public sealed class AcceptanceRegimeFeatureTests
{
    [Test]
    public async Task Section03_normal_norwegian_day_replays_the_routine_without_flex_or_compliance()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var ada = await fixture.ConnectAsync("ada");
        using var auditor = await fixture.ConnectAsync("audrey");

        var created = await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "ada",
            new DateOnly(2026, 10, 1),
            WorkRegistrationIntent.WorkedAsScheduled,
            [],
            null,
            "Worked as scheduled."));
        var workDay = await ada.GetWorkDayAsync("ada", new DateOnly(2026, 10, 1));

        await Assert.That(created.Intervals).IsEmpty();
        await Assert.That(workDay.Registration!.Intent).IsEqualTo(WorkRegistrationIntent.WorkedAsScheduled);
        await Assert.That(workDay.ExpectedWork).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(workDay.ActualWorked).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(workDay.Dimensions).IsEmpty();
        await Assert.That(workDay.ComplianceIndicators).IsEmpty();
        await Assert.That(workDay.LedgerTransactions).Count().IsEqualTo(1);
        await Assert.That(workDay.Configuration.Id).IsEqualTo(created.ConfigurationSnapshotId);
    }

    [Test]
    public async Task Section04_honest_split_day_preserves_the_gap_and_note()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var ada = await fixture.ConnectAsync("ada");
        using var bob = await fixture.ConnectAsync("bob");

        await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "ada",
            new DateOnly(2026, 10, 2),
            WorkRegistrationIntent.ManualRegistration,
            [
                Interval(2026, 10, 2, 8, 0, 12, 0),
                Interval(2026, 10, 2, 16, 0, 19, 30),
            ],
            null,
            "Dentist and family appointment."));
        await bob.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "bob",
            new DateOnly(2026, 10, 4),
            WorkRegistrationIntent.ManualRegistration,
            [
                new WorkIntervalRequest(
                    LocalTimestamp(new DateOnly(2026, 10, 4), new TimeOnly(18, 0)),
                    LocalTimestamp(new DateOnly(2026, 10, 5), new TimeOnly(2, 0))),
            ],
            null,
            "Evening shift."));

        var split = await ada.GetWorkDayAsync("ada", new DateOnly(2026, 10, 2));
        var midnight = await bob.GetWorkDayAsync("bob", new DateOnly(2026, 10, 4));

        await Assert.That(split.WorkedIntervals).Count().IsEqualTo(2);
        await Assert.That(split.ActualWorked).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(split.RoutineDifferences).Contains(difference => difference.Kind == "SplitWorkDay");
        await Assert.That(split.Registration!.Note).IsEqualTo("Dentist and family appointment.");
        await Assert.That(midnight.WorkedIntervals).Count().IsEqualTo(1);
        await Assert.That(midnight.ActualWorked).IsEqualTo(TimeSpan.FromHours(8));
        await Assert.That(midnight.Registration!.NominalDate).IsEqualTo(new DateOnly(2026, 10, 4));
    }

    [Test]
    public async Task Section06_calendar_explanation_keeps_holiday_entitlement_and_locale_snapshots()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var bob = await fixture.ConnectAsync("bob");
        using var ada = await fixture.ConnectAsync("ada");
        using var pierre = await fixture.ConnectAsync("pierre");
        using var anna = await fixture.ConnectAsync("anna");
        using var liisa = await fixture.ConnectAsync("liisa");

        var christmas = await bob.GetWorkDayAsync("bob", new DateOnly(2026, 12, 25));
        var christmasEve = await ada.GetWorkDayAsync("ada", new DateOnly(2026, 12, 24));
        var pierreBefore = await pierre.GetWorkDayAsync("pierre", new DateOnly(2026, 5, 14));
        var pierreAfter = await pierre.GetWorkDayAsync("pierre", new DateOnly(2026, 5, 15));
        var norway = await ada.GetWorkDayAsync("ada", new DateOnly(2026, 5, 1));
        var france = await pierre.GetWorkDayAsync("pierre", new DateOnly(2026, 5, 1));
        var poland = await anna.GetWorkDayAsync("anna", new DateOnly(2026, 5, 3));
        var finland = await liisa.GetWorkDayAsync("liisa", new DateOnly(2026, 12, 6));

        await Assert.That(christmas.IsWorkingDay).IsTrue();
        await Assert.That(christmas.OrganisationId).IsEqualTo("nordvik-station");
        await Assert.That(christmas.PaidEntitlement).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(christmas.Tags).Contains(tag => tag.Contains("PublicHoliday", StringComparison.Ordinal));
        await Assert.That(christmas.AppliedRules).Contains(rule =>
            rule.LayerKind == "National" &&
            rule.HolidayId == "førstejuledag" &&
            rule.SourcePackage == "PublicHoliday");
        await Assert.That(christmasEve.ExpectedWork).IsEqualTo(TimeSpan.Zero);
        await Assert.That(christmasEve.PaidEntitlement).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(christmasEve.OrganisationId).IsEqualTo("nordvik-office");
        await Assert.That(pierreBefore.Configuration.CalendarVersion).IsEqualTo("agreement-v1");
        await Assert.That(pierreAfter.Configuration.CalendarVersion).IsEqualTo("agreement-v2");
        await Assert.That(pierreBefore.Configuration.TimeZoneId).IsEqualTo("Europe/Paris");
        await Assert.That(norway.Tags).Contains(tag => tag.Contains("PublicHoliday", StringComparison.Ordinal));
        await Assert.That(norway.Tags).DoesNotContain(tag => tag.Contains("Travail", StringComparison.Ordinal));
        await Assert.That(france.Tags).Contains(tag => tag.Contains("PublicHoliday", StringComparison.Ordinal));
        await Assert.That(france.Tags).Contains(tag => tag.Contains("Travail", StringComparison.Ordinal));
        await Assert.That(poland.Tags).Contains(tag => tag.Contains("PublicHoliday", StringComparison.Ordinal));
        await Assert.That(finland.Tags).Contains(tag => tag.Contains("PublicHoliday", StringComparison.Ordinal));
    }

    [Test]
    public async Task Section17_long_day_is_accepted_and_reported_as_information()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var ada = await fixture.ConnectAsync("ada");

        await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "ada",
            new DateOnly(2026, 10, 6),
            WorkRegistrationIntent.ManualRegistration,
            [
                Interval(2026, 10, 6, 8, 0, 12, 0),
                Interval(2026, 10, 6, 12, 30, 16, 45),
            ],
            null,
            "Fifteen minutes retained as a truthful extra interval."));
        await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "ada",
            new DateOnly(2026, 10, 7),
            WorkRegistrationIntent.ManualRegistration,
            [
                Interval(2026, 10, 7, 7, 0, 12, 0),
                Interval(2026, 10, 7, 13, 0, 22, 0),
            ],
            null,
            "Long day for health-pattern reporting."));

        var workDay = await ada.GetWorkDayAsync("ada", new DateOnly(2026, 10, 6));
        var longDay = await ada.GetWorkDayAsync("ada", new DateOnly(2026, 10, 7));
        using var manager = await fixture.ConnectAsync("alice");
        var health = await manager.GetHealthReportAsync();

        var postings = workDay.LedgerTransactions.Single().Postings;
        await Assert.That(postings).Contains(posting =>
            posting.Account == "EmployeeFlex" &&
            posting.SignedDuration == TimeSpan.FromMinutes(45));
        await Assert.That(postings).Contains(posting =>
            posting.Account == "OrganisationControl" &&
            posting.SignedDuration == TimeSpan.FromMinutes(-45));
        await Assert.That(longDay.ComplianceIndicators).Contains(indicator =>
            indicator.Code == "worked-duration.above-threshold");
        await Assert.That(health.Description).DoesNotContain("Top employees");
        await Assert.That(health.Rows).Contains(row => row.Code == "worked-duration.above-threshold");
    }

    [Test]
    public async Task Section10_and_11_dimensions_and_correction_append_history_without_changing_actual_work()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var ada = await fixture.ConnectAsync("ada");

        var original = await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "ada",
            new DateOnly(2026, 10, 10),
            WorkRegistrationIntent.ManualRegistration,
            [
                Interval(2026, 10, 10, 8, 0, 12, 0),
                Interval(2026, 10, 10, 12, 30, 16, 45),
            ],
            null,
            "Original assertion."));
        await ada.RecordDimensionAssignmentAsync(
            "ada",
            new RecordDimensionAssignmentRequest(
                "ada",
                new DateOnly(2026, 10, 10),
                "customer",
                "acme",
                [Interval(2026, 10, 10, 8, 0, 12, 0)],
                null));
        await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "ada",
            new DateOnly(2026, 10, 10),
            WorkRegistrationIntent.Correction,
            [
                Interval(2026, 10, 10, 8, 0, 12, 0),
                Interval(2026, 10, 10, 12, 30, 16, 15),
            ],
            original.Id,
            "Removed fifteen minutes from the assertion."));

        var workDay = await ada.GetWorkDayAsync("ada", new DateOnly(2026, 10, 10));

        await Assert.That(workDay.ActualWorked).IsEqualTo(TimeSpan.FromHours(7.75));
        await Assert.That(workDay.Dimensions).Contains(measure =>
            measure.DimensionId == "customer" &&
            measure.ValueId == "acme" &&
            measure.Source == "Manual");
        await Assert.That(workDay.LedgerTransactions).Count().IsEqualTo(2);
        await Assert.That(workDay.LedgerTransactions).Contains(transaction =>
            transaction.Postings.Any(posting =>
                posting.Account == "EmployeeFlex" &&
                posting.SignedDuration == TimeSpan.FromMinutes(-15)));
        await Assert.That(workDay.Registration!.CorrectsRegistrationId).IsEqualTo(original.Id);
    }

    [Test]
    public async Task Section11_ledger_correction_keeps_both_balanced_transactions_and_current_saldo()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var ada = await fixture.ConnectAsync("ada");
        var original = await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "ada",
            new DateOnly(2026, 10, 27),
            WorkRegistrationIntent.ManualRegistration,
            [
                Interval(2026, 10, 27, 8, 0, 12, 0),
                Interval(2026, 10, 27, 12, 30, 16, 45),
            ],
            null,
            "Eight hours and fifteen minutes."));
        await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "ada",
            new DateOnly(2026, 10, 27),
            WorkRegistrationIntent.Correction,
            [
                Interval(2026, 10, 27, 8, 0, 12, 0),
                Interval(2026, 10, 27, 12, 30, 16, 30),
            ],
            original.Id,
            "Correction removes fifteen minutes."));

        var workDay = await ada.GetWorkDayAsync("ada", new DateOnly(2026, 10, 27));
        var registrations = await ada.GetWorkRegistrationsAsync("ada");
        var flexSaldo = workDay.LedgerTransactions
            .SelectMany(transaction => transaction.Postings)
            .Where(posting => posting.Account == "EmployeeFlex")
            .Aggregate(TimeSpan.Zero, (total, posting) => total + posting.SignedDuration);

        await Assert.That(registrations).Contains(registration => registration.Id == original.Id);
        await Assert.That(workDay.LedgerTransactions).Count().IsEqualTo(2);
        await Assert.That(flexSaldo).IsEqualTo(TimeSpan.FromMinutes(30));
        await Assert.That(workDay.LedgerTransactions).Contains(transaction =>
            transaction.Postings.Any(posting =>
                posting.Account == "EmployeeFlex" &&
                posting.SignedDuration == TimeSpan.FromMinutes(-15)));
    }

    [Test]
    public async Task Section12_four_stage_review_uses_real_users_and_preserves_action_history()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var ada = await fixture.ConnectAsync("ada");
        using var alice = await fixture.ConnectAsync("alice");
        using var charlie = await fixture.ConnectAsync("charlie");
        using var helen = await fixture.ConnectAsync("helen");

        var periodId = await ada.CreateReviewPeriodAsync(new CreateReviewPeriodRequest(
            "ada",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31)));
        await ada.RecordReviewActionAsync(periodId, new RecordReviewActionRequest("Submit", null, "October submitted.", null));
        await alice.RecordReviewActionAsync(periodId, new RecordReviewActionRequest("Approve", null, "Team review.", null));
        await charlie.RecordReviewActionAsync(periodId, new RecordReviewActionRequest("Approve", null, "Division review.", null));
        await helen.RecordReviewActionAsync(periodId, new RecordReviewActionRequest("Approve", null, "HR final review.", null));

        var review = await ada.GetReviewAsync(periodId);

        await Assert.That(review.PolicyId).IsEqualTo("cascading-approval");
        await Assert.That(review.State).IsEqualTo("Approved");
        await Assert.That(review.Actions).Count().IsEqualTo(4);
        await Assert.That(review.Actions).Contains(action => action.ActorId == "alice" && action.ApprovalLevel == 1);
        await Assert.That(review.Actions).Contains(action => action.ActorId == "charlie" && action.ApprovalLevel == 2);
        await Assert.That(review.Stages.All(stage => stage.IsComplete)).IsTrue();
    }

    [Test]
    public async Task Section22_stale_manager_review_is_rejected_after_an_employee_appends_a_correction()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var ada = await fixture.ConnectAsync("ada");
        using var alice = await fixture.ConnectAsync("alice");

        var original = await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "ada",
            new DateOnly(2026, 10, 11),
            WorkRegistrationIntent.ManualRegistration,
            [Interval(2026, 10, 11, 8, 0, 16, 0)],
            null,
            "Original."));
        var periodId = await ada.CreateReviewPeriodAsync(new CreateReviewPeriodRequest(
            "ada",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31)));
        await ada.RecordReviewActionAsync(periodId, new RecordReviewActionRequest("Submit", null, null, null));
        var opened = await alice.GetReviewAsync(periodId);
        await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "ada",
            new DateOnly(2026, 10, 11),
            WorkRegistrationIntent.Correction,
            [Interval(2026, 10, 11, 8, 0, 15, 30)],
            original.Id,
            "Correction appended while review was open."));

        var rejected = await CaptureStatusAsync(() =>
            alice.RecordReviewActionAsync(
                periodId,
                new RecordReviewActionRequest("Approve", 1, "Stale approval.", opened.JournalHead)));
        var current = await alice.GetReviewAsync(periodId);

        await Assert.That(rejected).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(current.ChangedAfterApproval).IsFalse();
        await Assert.That(current.Actions).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Section20_and_21_auditor_is_read_only_and_manager_scope_is_checked_by_http()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var auditor = await fixture.ConnectAsync("audrey");
        using var alice = await fixture.ConnectAsync("alice");
        using var bob = await fixture.ConnectAsync("bob");

        var health = await auditor.GetHealthReportAsync();
        var auditorWrite = await CaptureForbiddenAsync(() =>
            auditor.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
                "ada",
                new DateOnly(2026, 10, 8),
                WorkRegistrationIntent.WorkedAsScheduled,
                [],
                null,
                "Auditor must not write.")));
        var managerScope = await CaptureForbiddenAsync(() =>
            alice.GetWorkDayAsync("bob", new DateOnly(2026, 10, 8)));

        await Assert.That(health).IsNotNull();
        await Assert.That(auditorWrite).IsTrue();
        await Assert.That(managerScope).IsTrue();
        await Assert.That(bob).IsNotNull();
    }

    [Test]
    public async Task Section24_json_restart_replays_the_same_workday_and_configuration_snapshot()
    {
        var dataPath = Path.Combine(
            Path.GetTempPath(),
            "NovolisHoursAcceptance",
            Guid.NewGuid().ToString("N"));
        try
        {
            Guid snapshotId;
            Guid originalId;
            await using (var first = await HoursAcceptanceFixture.StartAsync("json", dataPath))
            {
                using var ada = await first.ConnectAsync("ada");
                var original = await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
                    "ada",
                    new DateOnly(2026, 10, 9),
                    WorkRegistrationIntent.ManualRegistration,
                    [Interval(2026, 10, 9, 8, 0, 16, 15)],
                    null,
                    "Persisted original assertion."));
                originalId = original.Id;
                await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
                    "ada",
                    new DateOnly(2026, 10, 9),
                    WorkRegistrationIntent.Correction,
                    [Interval(2026, 10, 9, 8, 0, 16, 0)],
                    original.Id,
                    "Persisted correction."));
                snapshotId = (await ada.GetWorkDayAsync("ada", new DateOnly(2026, 10, 9)))
                    .Configuration.Id;
            }

            await using (var second = await HoursAcceptanceFixture.StartAsync("json", dataPath))
            {
                using var ada = await second.ConnectAsync("ada");
                var replayed = await ada.GetWorkDayAsync("ada", new DateOnly(2026, 10, 9));
                await Assert.That(replayed.Configuration.Id).IsEqualTo(snapshotId);
                await Assert.That(replayed.ActualWorked).IsEqualTo(TimeSpan.FromHours(8));
                await Assert.That(replayed.Registration!.CorrectsRegistrationId).IsEqualTo(originalId);
                await Assert.That(replayed.LedgerTransactions).Count().IsEqualTo(2);
                await Assert.That((await ada.GetAuditAsync("ada"))
                    .Count(eventItem => eventItem.EventType == HoursEventType.WorkRegistrationRecorded))
                    .IsEqualTo(2);
            }
        }
        finally
        {
            if (Directory.Exists(dataPath))
            {
                Directory.Delete(dataPath, recursive: true);
            }
        }
    }

    [Test]
    public async Task Section24_azure_tables_replays_registration_and_correction_when_configured()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "NOVOLIS_HOURS_AZURITE_CONNECTION_STRING");
        Skip.Unless(
            !string.IsNullOrWhiteSpace(connectionString),
            "Set NOVOLIS_HOURS_AZURITE_CONNECTION_STRING to run the Azure Tables acceptance host.");

        var tablePrefix = $"novolishoursacceptance{Guid.CreateVersion7():N}";
        await using (var first = await HoursAcceptanceFixture.StartAsync(
                         "azure-tables",
                         azureTablePrefix: tablePrefix))
        {
            using var ada = await first.ConnectAsync("ada");
            var original = await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
                "ada",
                new DateOnly(2026, 10, 28),
                WorkRegistrationIntent.ManualRegistration,
                [Interval(2026, 10, 28, 8, 0, 16, 15)],
                null,
                "Azure original."));
            await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
                "ada",
                new DateOnly(2026, 10, 28),
                WorkRegistrationIntent.Correction,
                [Interval(2026, 10, 28, 8, 0, 16, 0)],
                original.Id,
                "Azure correction."));
        }

        await using (var second = await HoursAcceptanceFixture.StartAsync(
                         "azure-tables",
                         azureTablePrefix: tablePrefix))
        {
            using var ada = await second.ConnectAsync("ada");
            var replayed = await ada.GetWorkDayAsync("ada", new DateOnly(2026, 10, 28));

            await Assert.That(replayed.ActualWorked).IsEqualTo(TimeSpan.FromHours(8));
            await Assert.That(replayed.LedgerTransactions).Count().IsEqualTo(2);
            await Assert.That(await ada.GetWorkRegistrationsAsync("ada")).Count().IsEqualTo(2);
        }
    }

    [Test]
    public async Task Section05_midnight_shift_is_one_workday_and_points_to_one_audit_fact()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var bob = await fixture.ConnectAsync("bob");
        var created = await bob.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "bob",
            new DateOnly(2026, 10, 4),
            WorkRegistrationIntent.ManualRegistration,
            [
                new WorkIntervalRequest(
                    LocalTimestamp(new DateOnly(2026, 10, 4), new TimeOnly(18, 0)),
                    LocalTimestamp(new DateOnly(2026, 10, 5), new TimeOnly(2, 0))),
            ],
            null,
            "One overnight shift."));

        var workDay = await bob.GetWorkDayAsync("bob", new DateOnly(2026, 10, 4));
        var audit = await bob.GetAuditAsync("bob");

        await Assert.That(workDay.WorkedIntervals).Count().IsEqualTo(1);
        await Assert.That(workDay.ActualWorked).IsEqualTo(TimeSpan.FromHours(8));
        await Assert.That(workDay.ComplianceIndicators).Contains(indicator => indicator.Code == "work.night");
        await Assert.That(audit.Count(eventItem =>
                eventItem.EventType == HoursEventType.WorkRegistrationRecorded))
            .IsEqualTo(1);
        await Assert.That(audit).Contains(eventItem =>
            eventItem.PayloadReference == created.Id.ToString("D"));
    }

    [Test]
    public async Task Section07_christmas_eve_keeps_paid_entitlement_separate_from_actual_work()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var ada = await fixture.ConnectAsync("ada");

        var empty = await ada.GetWorkDayAsync("ada", new DateOnly(2026, 12, 24));
        await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "ada",
            new DateOnly(2026, 12, 24),
            WorkRegistrationIntent.ManualRegistration,
            [Interval(2026, 12, 24, 9, 0, 12, 0)],
            null,
            "Actual Christmas Eve work."));
        var worked = await ada.GetWorkDayAsync("ada", new DateOnly(2026, 12, 24));

        await Assert.That(empty.Registration).IsNull();
        await Assert.That(empty.ExpectedWork).IsEqualTo(TimeSpan.Zero);
        await Assert.That(empty.PaidEntitlement).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(worked.ActualWorked).IsEqualTo(TimeSpan.FromHours(3));
        await Assert.That(worked.PaidEntitlement).IsEqualTo(TimeSpan.FromHours(7.5));
    }

    [Test]
    public async Task Section08_national_calendar_publication_isolated_to_its_employee_stack()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var ada = await fixture.ConnectAsync("ada");
        using var pierre = await fixture.ConnectAsync("pierre");
        using var anna = await fixture.ConnectAsync("anna");
        using var liisa = await fixture.ConnectAsync("liisa");
        using var helen = await fixture.ConnectAsync("helen");

        var adaBefore = await ada.GetWorkDayAsync("ada", new DateOnly(2026, 5, 1));
        var france = await pierre.GetWorkDayAsync("pierre", new DateOnly(2026, 5, 1));
        var poland = await anna.GetWorkDayAsync("anna", new DateOnly(2026, 5, 3));
        var finland = await liisa.GetWorkDayAsync("liisa", new DateOnly(2026, 12, 6));
        await helen.PublishConfigurationAsync("pierre", new PublishConfigurationRequest(
            new DateOnly(2026, 5, 15)));
        var adaAfter = await ada.GetWorkDayAsync("ada", new DateOnly(2026, 5, 1));

        await Assert.That(adaBefore.OrganisationId).IsEqualTo("nordvik-office");
        await Assert.That(adaBefore.Tags).Contains(tag => tag.Contains("PublicHoliday", StringComparison.Ordinal));
        await Assert.That(adaBefore.Tags).DoesNotContain(tag => tag.Contains("Travail", StringComparison.Ordinal));
        await Assert.That(france.OrganisationId).IsEqualTo("atelier-curie");
        await Assert.That(france.Tags).Contains(tag => tag.Contains("PublicHoliday", StringComparison.Ordinal));
        await Assert.That(poland.Tags).Contains(tag => tag.Contains("PublicHoliday", StringComparison.Ordinal));
        await Assert.That(finland.Tags).Contains(tag => tag.Contains("PublicHoliday", StringComparison.Ordinal));
        await Assert.That(adaAfter.Configuration.Id).IsEqualTo(adaBefore.Configuration.Id);
        await Assert.That(adaAfter.AppliedRules.Select(rule => rule.RuleId))
            .IsEquivalentTo(adaBefore.AppliedRules.Select(rule => rule.RuleId));
        await Assert.That(adaAfter.AppliedRules.Select(rule => rule.LayerKind))
            .IsEquivalentTo(adaBefore.AppliedRules.Select(rule => rule.LayerKind));
    }

    [Test]
    public async Task Section09_midyear_agreement_change_keeps_each_day_on_its_snapshot()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var pierre = await fixture.ConnectAsync("pierre");
        var before = await pierre.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "pierre",
            new DateOnly(2026, 5, 14),
            WorkRegistrationIntent.ManualRegistration,
            [Interval(2026, 5, 14, 9, 0, 17, 0)],
            null,
            "Agreement v1 day."));
        var after = await pierre.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "pierre",
            new DateOnly(2026, 5, 15),
            WorkRegistrationIntent.ManualRegistration,
            [Interval(2026, 5, 15, 9, 0, 17, 0)],
            null,
            "Agreement v2 day."));
        var beforeDay = await pierre.GetWorkDayAsync("pierre", new DateOnly(2026, 5, 14));
        var afterDay = await pierre.GetWorkDayAsync("pierre", new DateOnly(2026, 5, 15));

        await Assert.That(before.ConfigurationSnapshotId).IsNotEqualTo(after.ConfigurationSnapshotId);
        await Assert.That(beforeDay.Configuration.CalendarVersion).IsEqualTo("agreement-v1");
        await Assert.That(afterDay.Configuration.CalendarVersion).IsEqualTo("agreement-v2");
        await Assert.That(beforeDay.Configuration.Id).IsEqualTo(before.ConfigurationSnapshotId);
        await Assert.That(afterDay.Configuration.Id).IsEqualTo(after.ConfigurationSnapshotId);
    }

    [Test]
    public async Task Section13_employer_entry_is_attributed_to_manager_and_acknowledged_by_employee()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var bob = await fixture.ConnectAsync("bob");
        using var nina = await fixture.ConnectAsync("nina");
        var periodId = await bob.CreateReviewPeriodAsync(new CreateReviewPeriodRequest(
            "bob",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31)));
        var employerEntry = await nina.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "bob",
            new DateOnly(2026, 10, 13),
            WorkRegistrationIntent.ManualRegistration,
            [Interval(2026, 10, 13, 8, 0, 16, 0)],
            null,
            "Employer-entered time."));
        await bob.RecordReviewActionAsync(
            periodId,
            new RecordReviewActionRequest("Acknowledge", null, "I acknowledge the employer entry.", null));

        var workDay = await bob.GetWorkDayAsync("bob", new DateOnly(2026, 10, 13));
        var review = await bob.GetReviewAsync(periodId);

        await Assert.That(employerEntry.Source).IsEqualTo(WorkRecordSource.Employer.ToString());
        await Assert.That(employerEntry.RecordedBy).IsEqualTo("nina");
        await Assert.That(workDay.Registration!.Source).IsEqualTo(WorkRecordSource.Employer.ToString());
        await Assert.That(workDay.Registration.RecordedBy).IsEqualTo("nina");
        await Assert.That(review.PolicyId).IsEqualTo("employer-acknowledged");
        await Assert.That(review.State).IsEqualTo("Approved");
        await Assert.That(review.Stages.Single().Id).IsEqualTo("employee-acknowledge");
        await Assert.That(review.Actions).Contains(action =>
            action.Kind == "Acknowledge" && action.ActorId == "bob");
    }

    [Test]
    public async Task Section14_dispute_keeps_employer_and_employee_assertions_after_hr_resolution()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var bob = await fixture.ConnectAsync("bob");
        using var nina = await fixture.ConnectAsync("nina");
        using var helen = await fixture.ConnectAsync("helen");
        var periodId = await bob.CreateReviewPeriodAsync(new CreateReviewPeriodRequest(
            "bob",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31)));
        var employer = await nina.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "bob",
            new DateOnly(2026, 10, 14),
            WorkRegistrationIntent.ManualRegistration,
            [Interval(2026, 10, 14, 8, 0, 15, 30)],
            null,
            "Employer assertion."));
        var employee = await bob.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "bob",
            new DateOnly(2026, 10, 14),
            WorkRegistrationIntent.ManualRegistration,
            [Interval(2026, 10, 14, 8, 0, 16, 30)],
            null,
            "Employee assertion."));
        await bob.RecordReviewActionAsync(
            periodId,
            new RecordReviewActionRequest("Dispute", null, "I worked until 16:30.", null));
        await helen.RecordReviewActionAsync(
            periodId,
            new RecordReviewActionRequest("Resolve", null, "HR recorded the disagreement.", null));

        var registrations = await bob.GetWorkRegistrationsAsync("bob");
        var review = await bob.GetReviewAsync(periodId);

        await Assert.That(registrations).Contains(registration => registration.Id == employer.Id);
        await Assert.That(registrations).Contains(registration => registration.Id == employee.Id);
        await Assert.That(review.Actions).Contains(action => action.Kind == "Dispute");
        await Assert.That(review.Actions).Contains(action =>
            action.Kind == "Resolve" && action.ActorId == "helen");
    }

    [Test]
    public async Task Section15_integration_stamps_remain_distinct_from_an_employee_correction()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var integration = await fixture.ConnectAsync("system");
        using var ada = await fixture.ConnectAsync("ada");
        var stamped = await integration.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "ada",
            new DateOnly(2026, 10, 15),
            WorkRegistrationIntent.ManualRegistration,
            [
                Interval(2026, 10, 15, 7, 58, 12, 1),
                Interval(2026, 10, 15, 12, 29, 16, 7),
            ],
            null,
            "Stamping integration assertion.",
            WorkRecordSource.Integration));
        var correction = await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "ada",
            new DateOnly(2026, 10, 15),
            WorkRegistrationIntent.Correction,
            [
                Interval(2026, 10, 15, 8, 0, 12, 0),
                Interval(2026, 10, 15, 12, 30, 16, 0),
            ],
            stamped.Id,
            "Employee correction of stamp interpretation."));

        var registrations = await ada.GetWorkRegistrationsAsync("ada");

        await Assert.That(registrations).Contains(registration =>
            registration.Id == stamped.Id &&
            registration.Source == WorkRecordSource.Integration.ToString() &&
            registration.RecordedBy == "system");
        await Assert.That(registrations).Contains(registration =>
            registration.Id == correction.Id &&
            registration.Source == WorkRecordSource.Employee.ToString() &&
            registration.RecordedBy == "ada");
    }

    [Test]
    public async Task Section16_correction_after_final_approval_reopens_current_steps_and_keeps_old_actions()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var ada = await fixture.ConnectAsync("ada");
        using var alice = await fixture.ConnectAsync("alice");
        using var charlie = await fixture.ConnectAsync("charlie");
        using var helen = await fixture.ConnectAsync("helen");
        var original = await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "ada",
            new DateOnly(2026, 10, 16),
            WorkRegistrationIntent.ManualRegistration,
            [Interval(2026, 10, 16, 8, 0, 16, 0)],
            null,
            "Original October fact."));
        var periodId = await ada.CreateReviewPeriodAsync(new CreateReviewPeriodRequest(
            "ada",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31)));
        await ada.RecordReviewActionAsync(periodId, new RecordReviewActionRequest("Submit", null, null, null));
        await alice.RecordReviewActionAsync(periodId, new RecordReviewActionRequest("Approve", 1, null, null));
        await charlie.RecordReviewActionAsync(periodId, new RecordReviewActionRequest("Approve", 2, null, null));
        await helen.RecordReviewActionAsync(periodId, new RecordReviewActionRequest("Approve", null, null, null));
        await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "ada",
            new DateOnly(2026, 10, 16),
            WorkRegistrationIntent.Correction,
            [Interval(2026, 10, 16, 8, 0, 15, 45)],
            original.Id,
            "Truthful post-approval correction."));

        var review = await ada.GetReviewAsync(periodId);

        await Assert.That(review.ChangedAfterApproval).IsTrue();
        await Assert.That(review.Actions).Count().IsEqualTo(4);
        await Assert.That(review.Actions).Contains(action => action.ActorId == "charlie");
        await Assert.That(review.Stages).Contains(stage => !stage.IsComplete);
    }

    [Test]
    public async Task Section18_health_report_aggregates_patterns_without_person_ranking()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var ada = await fixture.ConnectAsync("ada");
        using var bob = await fixture.ConnectAsync("bob");
        using var auditor = await fixture.ConnectAsync("audrey");
        await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "ada",
            new DateOnly(2026, 10, 20),
            WorkRegistrationIntent.ManualRegistration,
            [Interval(2026, 10, 20, 22, 0, 23, 0)],
            null,
            "Late Team A work."));
        await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "ada",
            new DateOnly(2026, 10, 21),
            WorkRegistrationIntent.ManualRegistration,
            [Interval(2026, 10, 21, 7, 0, 8, 0)],
            null,
            "Short rest after Team A late work."));
        await bob.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "bob",
            new DateOnly(2026, 10, 20),
            WorkRegistrationIntent.ManualRegistration,
            [Interval(2026, 10, 20, 8, 0, 16, 0)],
            null,
            "Team B ordinary work."));
        var report = await auditor.GetHealthReportAsync();

        await Assert.That(report.Rows).Contains(row => row.Code == "rest.daily-below-threshold");
        await Assert.That(report.Rows.Single(row => row.Code == "rest.daily-below-threshold").WorkDays)
            .Contains("ada:2026-10-21");
        await Assert.That(report.Description).DoesNotContain("Top employees");
        await Assert.That(report.Description).DoesNotContain("violating");
    }

    [Test]
    public async Task Section19_business_pressure_uses_overtime_overlap_and_drilldown_keys()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var ada = await fixture.ConnectAsync("ada");
        using var auditor = await fixture.ConnectAsync("audrey");
        await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "ada",
            new DateOnly(2026, 10, 22),
            WorkRegistrationIntent.ManualRegistration,
            [
                Interval(2026, 10, 22, 7, 0, 12, 0),
                Interval(2026, 10, 22, 13, 0, 20, 0),
            ],
            null,
            "Extra work allocated to customers."));
        await ada.RecordDimensionAssignmentAsync(
            "ada",
            new RecordDimensionAssignmentRequest(
                "ada",
                new DateOnly(2026, 10, 22),
                "customer",
                "acme",
                [Interval(2026, 10, 22, 16, 30, 20, 0)],
                null));
        await ada.RecordDimensionAssignmentAsync(
            "ada",
            new RecordDimensionAssignmentRequest(
                "ada",
                new DateOnly(2026, 10, 22),
                "customer",
                "beta",
                [Interval(2026, 10, 22, 7, 0, 8, 0)],
                null));

        var report = await auditor.GetBusinessPressureReportAsync();
        var acme = report.Rows.Single(row => row.ValueId == "acme");
        var beta = report.Rows.Single(row => row.ValueId == "beta");

        await Assert.That(acme.Duration).IsGreaterThan(beta.Duration);
        await Assert.That(acme.WorkDays).Contains("ada:2026-10-22");
        await Assert.That(report.Description).Contains("overlap");
        await Assert.That(report.Description).DoesNotContain("caused");
    }

    [Test]
    public async Task Section20_auditor_can_read_the_trail_but_all_mutations_are_forbidden()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var ada = await fixture.ConnectAsync("ada");
        using var auditor = await fixture.ConnectAsync("audrey");
        var original = await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "ada",
            new DateOnly(2026, 10, 23),
            WorkRegistrationIntent.ManualRegistration,
            [Interval(2026, 10, 23, 8, 0, 16, 0)],
            null,
            "Audited fact."));
        var periodId = await ada.CreateReviewPeriodAsync(new CreateReviewPeriodRequest(
            "ada",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31)));

        await Assert.That((await auditor.GetWorkDayAsync("ada", new DateOnly(2026, 10, 23))).Registration).IsNotNull();
        await Assert.That(await auditor.GetAuditAsync("ada")).IsNotEmpty();
        await Assert.That(await CaptureForbiddenAsync(() =>
            auditor.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
                "ada",
                new DateOnly(2026, 10, 24),
                WorkRegistrationIntent.WorkedAsScheduled,
                [],
                null,
                "Forbidden.")))).IsTrue();
        await Assert.That(await CaptureForbiddenAsync(() =>
            auditor.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
                "ada",
                new DateOnly(2026, 10, 23),
                WorkRegistrationIntent.Correction,
                [Interval(2026, 10, 23, 8, 0, 15, 0)],
                original.Id,
                "Forbidden correction.")))).IsTrue();
        await Assert.That(await CaptureForbiddenAsync(() =>
            auditor.RecordReviewActionAsync(
                periodId,
                new RecordReviewActionRequest("Approve", 1, null, null)))).IsTrue();
        await Assert.That(await CaptureForbiddenAsync(() =>
            auditor.RecordReviewActionAsync(
                periodId,
                new RecordReviewActionRequest("Resolve", null, null, null)))).IsTrue();
        await Assert.That(await CaptureForbiddenAsync(() =>
            auditor.PublishConfigurationAsync(
                "ada",
                new PublishConfigurationRequest(new DateOnly(2026, 10, 25))))).IsTrue();
    }

    [Test]
    public async Task Section21_manager_scope_rejects_team_b_employee_and_report_filters_over_http()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var alice = await fixture.ConnectAsync("alice");
        using var bob = await fixture.ConnectAsync("bob");

        await Assert.That(await CaptureForbiddenAsync(() =>
            alice.GetWorkDayAsync("bob", new DateOnly(2026, 10, 24)))).IsTrue();
        await Assert.That(await CaptureForbiddenAsync(() =>
            alice.GetWorkRegistrationsAsync("bob"))).IsTrue();
        await Assert.That(await CaptureForbiddenAsync(() =>
            alice.GetHealthReportAsync(teamId: "team-b"))).IsTrue();
        await Assert.That(await CaptureForbiddenAsync(() =>
            alice.GetBusinessPressureReportAsync(teamId: "team-b"))).IsTrue();
        await Assert.That((await bob.GetWorkDayAsync("bob", new DateOnly(2026, 10, 24))).EmployeeId)
            .IsEqualTo("bob");
    }

    [Test]
    public async Task Section23_published_configuration_changes_future_days_and_preserves_monday()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var ada = await fixture.ConnectAsync("ada");
        using var helen = await fixture.ConnectAsync("helen");
        var monday = new DateOnly(2026, 10, 26);
        var tuesday = monday.AddDays(1);
        await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "ada",
            monday,
            WorkRegistrationIntent.WorkedAsScheduled,
            [],
            null,
            "Monday uses snapshot A."));
        var mondayBeforePublish = await ada.GetWorkDayAsync("ada", monday);
        var published = await helen.PublishConfigurationAsync(
            "ada",
            new PublishConfigurationRequest(tuesday));
        await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "ada",
            tuesday,
            WorkRegistrationIntent.WorkedAsScheduled,
            [],
            null,
            "Tuesday uses snapshot B."));

        var mondayAfterPublish = await ada.GetWorkDayAsync("ada", monday);
        var tuesdayAfterPublish = await ada.GetWorkDayAsync("ada", tuesday);

        await Assert.That(mondayAfterPublish.Configuration.Id)
            .IsEqualTo(mondayBeforePublish.Configuration.Id);
        await Assert.That(tuesdayAfterPublish.Configuration.Id).IsEqualTo(published.Id);
        await Assert.That(tuesdayAfterPublish.Configuration.CalendarVersion)
            .Contains("published");
        await Assert.That(mondayAfterPublish.AppliedRules.Select(rule => rule.CalendarVersion))
            .DoesNotContain(version => version.Contains("published", StringComparison.Ordinal));
    }

    private static WorkIntervalRequest Interval(
        int year,
        int month,
        int day,
        int startHour,
        int startMinute,
        int endHour,
        int endMinute) =>
        new(
            LocalTimestamp(new DateOnly(year, month, day), new TimeOnly(startHour, startMinute)),
            LocalTimestamp(new DateOnly(year, month, day), new TimeOnly(endHour, endMinute)));

    private static DateTimeOffset LocalTimestamp(DateOnly date, TimeOnly time)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo");
        }
        catch (TimeZoneNotFoundException)
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        }

        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    private static async Task<bool> CaptureForbiddenAsync(Func<Task> operation)
    {
        try
        {
            await operation();
            return false;
        }
        catch (HttpRequestException exception)
        {
            return exception.StatusCode == HttpStatusCode.Forbidden;
        }
    }

    private static async Task<HttpStatusCode?> CaptureStatusAsync(Func<Task> operation)
    {
        try
        {
            await operation();
            return null;
        }
        catch (HttpRequestException exception)
        {
            return exception.StatusCode;
        }
    }
}
