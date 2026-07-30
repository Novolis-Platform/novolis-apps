using SinsOfACapitalismTycoon.Universe.Mesh.Sins;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Default ordered day-end steps — enable/disable or reorder without editing the clock.</summary>
internal static class CampaignDayPipeline
{
  public static IReadOnlyList<ICampaignDayStep> Default { get; } =
  [
    new ClaimsDayStep(),
    new DriveMaintenanceDayStep(),
    new EscrowDayStep(),
    new MeshGameplayDayStep(),
    new DockFeesDayStep(),
    new LienDayStep(),
    new InsuranceDayStep(),
    new LastTrampDayStep(),
    new ReputationDayStep(),
    new DramaDayStep(),
    new TutorialDayStep(),
    new MeshBoardUnlockDayStep(),
    new StockoutDayStep(),
    new LastTrampEvaluateDayStep(),
  ];

  public static void RunDayEnd(CampaignPulseContext ctx)
  {
    foreach (var step in Default)
    {
      if (ctx.MaxSpeedThroughput && step is MeshGameplayDayStep or DramaDayStep)
      {
        continue;
      }

      step.TickDay(ctx);
    }
  }

  private sealed class ClaimsDayStep : ICampaignDayStep
  {
    public string Name => "claims";

    public void TickDay(CampaignPulseContext ctx) =>
      ClaimsPulse.TickDay(ctx.Sim, ctx.Ids.Registry, ctx.Ids, ctx.Milestones, ctx.Biographies, ctx.Claims);
  }

  private sealed class DriveMaintenanceDayStep : ICampaignDayStep
  {
    public string Name => "drive-maintenance";

    public void TickDay(CampaignPulseContext ctx) =>
      DriveMaintenancePulse.TickDay(ctx.Sim, ctx.Ids.Registry, ctx.Milestones);
  }

  private sealed class EscrowDayStep : ICampaignDayStep
  {
    public string Name => "escrow";

    public void TickDay(CampaignPulseContext ctx)
    {
      ctx.Ids.Escrow.TickDay(ctx.Sim, ctx.Ids, ctx.Milestones);
      foreach (var n in ctx.Ids.Escrow.DrainNotices())
      {
        ctx.Notices.Publish(
          "escrow",
          n.Kind,
          n.Detail,
          n.Day,
          n.CarrierFirmId,
          n.CarrierRegistryName,
          n.Amount);
      }
    }
  }

  private sealed class MeshGameplayDayStep : ICampaignDayStep
  {
    public string Name => "mesh-gameplay";

    public void TickDay(CampaignPulseContext ctx) =>
      ctx.Ids.Mesh = MeshGameplayPulse.TickDay(ctx.Ids.Mesh, ctx.Sim, ctx.Ids, ctx.Milestones, ctx.Notices);
  }

  private sealed class DockFeesDayStep : ICampaignDayStep
  {
    public string Name => "dock-fees";

    public void TickDay(CampaignPulseContext ctx) =>
      JumpBandGate.TickDockFees(ctx.Sim, ctx.Ids, ctx.Milestones);
  }

  private sealed class LienDayStep : ICampaignDayStep
  {
    public string Name => "lien";

    public void TickDay(CampaignPulseContext ctx) =>
      LienPulse.TickDay(ctx.Sim, ctx.Ids, ctx.Milestones);
  }

  private sealed class InsuranceDayStep : ICampaignDayStep
  {
    public string Name => "insurance";

    public void TickDay(CampaignPulseContext ctx) =>
      InsurancePulse.TickDay(ctx.Sim, ctx.Ids.Registry, ctx.Milestones, ctx.Credits);
  }

  private sealed class LastTrampDayStep : ICampaignDayStep
  {
    public string Name => "last-tramp-pressure";

    public void TickDay(CampaignPulseContext ctx)
    {
      if (ctx.Player.LastTrampMode)
      {
        LastTrampPressure.TickDay(ctx.Sim, ctx.Ids, ctx.Milestones);
      }
    }
  }

  private sealed class ReputationDayStep : ICampaignDayStep
  {
    public string Name => "reputation";

    public void TickDay(CampaignPulseContext ctx) =>
      ctx.Ids.Reputation.TickDay(ctx.Sim.State.Clock.Date.DayIndex);
  }

  private sealed class DramaDayStep : ICampaignDayStep
  {
    public string Name => "drama";

    public void TickDay(CampaignPulseContext ctx) => ctx.Drama.TickDayEnd(ctx.Sim);
  }

  private sealed class TutorialDayStep : ICampaignDayStep
  {
    public string Name => "tutorial";

    public void TickDay(CampaignPulseContext ctx)
    {
      ctx.Tutorial?.TickDayEnd(ctx.Sim);
      if (ctx.Player is { Enabled: true, SoftFailRaised: true })
      {
        ctx.Notices.Publish(
          "soft-fail",
          "soft-fail",
          $"{CampaignWorld.PlayerHullName} SoftFail",
          ctx.Sim.State.Clock.Date.DayIndex,
          ctx.Ids.Carrier,
          CampaignWorld.PlayerHullName);
      }
    }
  }

  private sealed class MeshBoardUnlockDayStep : ICampaignDayStep
  {
    public string Name => "mesh-board-unlock";

    public void TickDay(CampaignPulseContext ctx)
    {
      if (ctx.Player.Enabled)
      {
        MeshBoardUnlock.Sync(ctx.Player, ctx.Milestones);
      }
    }
  }

  private sealed class StockoutDayStep : ICampaignDayStep
  {
    public string Name => "stockout";

    public void TickDay(CampaignPulseContext ctx) =>
      CampaignRunner.ObserveFinalStockout(ctx.Sim, ctx.Ids, ctx.Milestones);
  }

  private sealed class LastTrampEvaluateDayStep : ICampaignDayStep
  {
    public string Name => "last-tramp-evaluate";

    public void TickDay(CampaignPulseContext ctx) => ctx.EvaluateLastTramp();
  }
}
