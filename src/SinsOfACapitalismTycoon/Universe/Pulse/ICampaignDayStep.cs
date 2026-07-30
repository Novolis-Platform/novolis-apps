namespace SinsOfACapitalismTycoon.Universe;

/// <summary>One ordered day-end commerce / story step. Register in <see cref="CampaignDayPipeline"/>.</summary>
internal interface ICampaignDayStep
{
  string Name { get; }

  void TickDay(CampaignPulseContext ctx);
}
