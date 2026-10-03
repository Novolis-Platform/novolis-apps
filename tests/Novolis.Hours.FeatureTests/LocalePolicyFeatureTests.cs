using Novolis.Hours.Domain;

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
}
