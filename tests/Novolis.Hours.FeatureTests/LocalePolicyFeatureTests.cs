using Novolis.Hours.Domain;
using Novolis.Time;
using Novolis.Time.Worktime;
using Novolis.Time.Worktime.Legal;

namespace Novolis.Hours.FeatureTests;

public sealed class LocalePolicyFeatureTests
{
    [Test]
    [Arguments("norway.private.flex", "NO")]
    [Arguments("norway.state.flex", "NO")]
    [Arguments("belgium.office.flex", "BE")]
    [Arguments("england.office.flex", "GB")]
    [Arguments("france.annualisation", "FR")]
    [Arguments("poland.okres-rozliczeniowy", "PL")]
    [Arguments("finland.liukuva-tyoaika", "FI")]
    public async Task Each_supported_locale_has_a_profile_calendar_template_and_non_blocking_legal_preset(
        string presetId,
        string countryCode)
    {
        var policy = HoursPolicyCatalog.Create(presetId, 2026);

        await Assert.That(policy.LegalPreset.Id).IsEqualTo(presetId);
        await Assert.That(policy.LegalPreset.CountryCode).IsEqualTo(countryCode);
        await Assert.That(policy.EmploymentSettings.Profile.DayHours).IsGreaterThan(TimeSpan.Zero);
        await Assert.That(policy.EmploymentSettings.Template.GetExpectedInterval(new DateOnly(2026, 10, 1))).IsNotNull();
        await Assert.That(policy.Calendar.IsWorkday(new DateOnly(2026, 10, 1))).IsTrue();
        await Assert.That(policy.LegalPreset.OvertimeAgreementMessage).IsNotEmpty();
    }

    [Test]
    public async Task A_tenant_message_overrides_only_the_displayed_agreement_instruction()
    {
        var original = HoursPolicyCatalog.Create("norway.private.flex", 2026);
        var configured = original with
        {
            LegalPreset = original.LegalPreset.WithOvertimeAgreementMessage(
                "Emergency compensation needs a documented manager agreement reference."),
        };

        await Assert.That(configured.LegalPreset.Id).IsEqualTo(original.LegalPreset.Id);
        await Assert.That(configured.LegalPreset.Citation).IsEqualTo(original.LegalPreset.Citation);
        await Assert.That(configured.LegalPreset.OvertimeAgreementMessage).Contains("documented manager agreement");
    }

    [Test]
    [Arguments("norway.private.flex")]
    [Arguments("norway.state.flex")]
    [Arguments("belgium.office.flex")]
    [Arguments("england.office.flex")]
    [Arguments("france.annualisation")]
    [Arguments("poland.okres-rozliczeniowy")]
    [Arguments("finland.liukuva-tyoaika")]
    public async Task Every_draft_locale_starter_retains_normal_long_holiday_and_settlement_evidence_without_gating_records(
        string presetId)
    {
        var policy = HoursPolicyCatalog.Create(presetId, 2026);
        var workday = new DateOnly(2026, 10, 1);
        var expected = WorktimeCalculator.CreateExpectedSnapshot(workday, policy.EmploymentSettings);
        var interval = expected.ExpectedInterval
            ?? throw new InvalidOperationException("The locale starter did not create an expected interval on a weekday.");
        var normal = new ActualWorkRecord(
            Guid.CreateVersion7(),
            workday,
            interval,
            policy.EmploymentSettings.Profile.Lunch,
            [],
            "Normal-day parity fixture.",
            hasManagerAgreement: true);
        var normalBalance = WorktimeCalculator.Calculate(normal, expected);

        var envelope = policy.EmploymentSettings.Profile.WorkingDayEnvelope;
        var compensation = new FinancialCompensationMark(
            new ClockInterval(envelope.Start, envelope.Start.AddHours(1)),
            "Long-day parity fixture.");
        var longRecord = new ActualWorkRecord(
            Guid.CreateVersion7(),
            workday,
            envelope,
            null,
            [compensation],
            "Long-day parity fixture.",
            hasManagerAgreement: false);
        var longBalance = WorktimeCalculator.Calculate(longRecord, expected);
        var longFirings = WorktimeLegalEvaluator.Evaluate(
            longRecord,
            longBalance,
            policy.EmploymentSettings.Profile,
            policy.LegalPreset);

        var holiday = WorktimeCalculator.CreateExpectedSnapshot(
            new DateOnly(2026, 1, 1),
            policy.EmploymentSettings);
        var normalization = FlexNormalization.Create(
            policy.LegalPreset.FlexCarryPolicy,
            policy.LegalPreset.FlexCarryPolicy.PositiveCarryCap + TimeSpan.FromHours(1));

        await Assert.That(normalBalance.FlexDelta).IsEqualTo(TimeSpan.Zero);
        await Assert.That(longBalance.Actual).IsGreaterThan(normalBalance.Actual);
        await Assert.That(longFirings.Single(firing => firing.RuleId == "financial-compensation.manager-agreement").Message)
            .IsEqualTo(policy.LegalPreset.OvertimeAgreementMessage);
        await Assert.That(holiday.ExpectedDuration).IsEqualTo(TimeSpan.Zero);
        await Assert.That(normalization.NormalizedUnusedFlex).IsEqualTo(TimeSpan.FromHours(1));
        await Assert.That(normalization.FinanciallyCompensated).IsEqualTo(TimeSpan.Zero);
        await Assert.That(policy.LegalPreset.Citation).IsNotEmpty();
        await Assert.That(policy.LegalPreset.Version).IsNotEmpty();
        await Assert.That(policy.LegalPreset.ReviewState).IsEqualTo(LegalReviewState.Draft);
    }
}
