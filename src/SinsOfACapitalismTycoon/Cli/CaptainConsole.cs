using System.Diagnostics;
using Novolis.Economy.Logistics;
using Novolis.Game.Session;
using Spectre.Console;
using SinsOfACapitalismTycoon.Universe;

namespace SinsOfACapitalismTycoon.Cli;

/// <summary>
/// Text captain desk — scriptable. Verbs mirror Avalonia:
/// status, spot, charters, manifest, accept N, depart, travel &lt;system&gt;,
/// refuse, wait, premium, overhaul, board mesh|dock, step, continue, resume, help, quit.
/// </summary>
internal static class CaptainConsole
{
  public static async Task<int> RunAsync(RunOptions options)
  {
    CampaignRunner.LiveSession session;
    if (!string.IsNullOrWhiteSpace(options.LoadSave))
    {
      var save = await CampaignSaveResolver.ResolveAsync(options.LoadSave).ConfigureAwait(false)
                 ?? throw new InvalidOperationException($"Save not found: {options.LoadSave}");
      Console.WriteLine($"Loading checkpoint {save.Id:N} · {save.Label} · d{save.DayIndex}");
      session = await CampaignRunner.LiveSession.FromSaveAsync(save).ConfigureAwait(false);
    }
    else
    {
      session = new CampaignRunner.LiveSession(
        options.Seed,
        options.DaysHours,
        options.Drama,
        playerControl: true,
        autopilot: options.Autopilot,
        localBoard: options.Board == JobBoardScope.Dock,
        lastTramp: options.LastTramp);
    }

    session.AwaitingDecision += () => PrintStatus(session, banner: true);
    session.DayEnded += () =>
    {
      if (session.IsWaitingForCaptain || session.IsComplete) return;
      var d = session.Agents.CarrierPulse.LastDecision;
      if (d.Contains("underway", StringComparison.OrdinalIgnoreCase)
          || d.Contains("reposition", StringComparison.OrdinalIgnoreCase)
          || d.Contains("awaiting departure", StringComparison.OrdinalIgnoreCase))
      {
        Console.Error.WriteLine($"… d{session.Sim.State.Clock.Date.DayIndex} {CampaignWorld.PlayerHullName}: {d}");
      }
    };

    var desk = new CaptainDeskService(session);
    await using var surface = SessionSurface.AttachAll(
      desk,
      preferredPipeName: SessionEndpoints.SinsPipeName);
    if (surface?.HttpBaseUrl is { } httpUrl)
      Console.Error.WriteLine($"session HTTP {httpUrl}");
    if (surface?.TcpPort is { } tcpPort)
      Console.Error.WriteLine($"session TCP jsonl 127.0.0.1:{tcpPort}");

    var runTask = Task.Run(async () =>
      await session.RunAsync(quiet: true, story: false).ConfigureAwait(false));

    while (!session.IsComplete && !session.IsWaitingForCaptain)
    {
      await Task.Delay(50).ConfigureAwait(false);
    }

    IEnumerable<string> commandSource = options.Playtest
      ? (options.LastTramp ? LastTrampPlaytestScript() : PlaytestScript())
      : !string.IsNullOrWhiteSpace(options.Commands)
        ? options.Commands.Split([';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        : ReadInteractive();

    var hauled = false;
    var traveled = false;
    var remoteReject = false;
    var exitCode = 0;

    foreach (var raw in commandSource)
    {
      if (session.IsComplete) break;
      var line = raw.Trim();
      if (line.Length == 0 || line.StartsWith('#')) continue;

      if (!IsFreeCommand(line) && !session.IsWaitingForCaptain && !session.IsComplete)
      {
        await WaitPause(session).ConfigureAwait(false);
      }

      var result = Handle(line, session, desk, ref hauled, ref traveled, ref remoteReject);
      if (result == HandleResult.Quit)
      {
        session.ResumeToHorizon();
        break;
      }

      if (result == HandleResult.Fail)
      {
        exitCode = 1;
        session.ResumeToHorizon();
        break;
      }

      if (result == HandleResult.WaitBerth)
      {
        var guard = 0;
        while (!session.IsComplete && guard++ < 48)
        {
          session.Player.DockBoardOnly = false;
          if (session.ListJobs().Any(j => j.AtOrigin)
              || CaptainJobBoard.ListLiveFreight(
                   session.Sim, session.Ids, session.Player.DefaultProfile, session.CurrentSystemId)
                 .Any(j => j.AtOrigin))
          {
            Console.WriteLine($"AT-DOCK spot ready @ {session.CurrentSystemId}");
            break;
          }

          Console.WriteLine(
            $"… waiting dock ({session.CurrentSystemId}) · {session.Agents.CarrierPulse.LastDecision}");
          session.Continue();
          await WaitPause(session).ConfigureAwait(false);
        }

        if (!session.ListJobs().Any(j => j.AtOrigin)
            && !CaptainJobBoard.ListLiveFreight(
                  session.Sim, session.Ids, session.Player.DefaultProfile, session.CurrentSystemId)
                .Any(j => j.AtOrigin))
        {
          Console.WriteLine("Timed out waiting for AT-DOCK spot");
          exitCode = 1;
          session.ResumeToHorizon();
          break;
        }

        continue;
      }

      if (result == HandleResult.Advanced)
      {
        await WaitPause(session).ConfigureAwait(false);
      }
    }

    if (!runTask.IsCompleted) session.ResumeToHorizon();
    try
    {
      await runTask.ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      Console.Error.WriteLine($"RUN FAILED: {ex}");
      Console.Out.Flush();
      Console.Error.Flush();
      return 1;
    }

    if (options.Playtest)
    {
      if (options.LastTramp)
      {
        var r = session.ToResult();
        var okLt = r.LastTrampWon && !r.LastTrampLost;
        var lineLt = okLt
          ? $"PLAYTEST LAST-TRAMP PASS — operable={r.Survival?.OperableCount} won={r.LastTrampWon}"
          : $"PLAYTEST LAST-TRAMP FAIL — operable={r.Survival?.OperableCount} won={r.LastTrampWon} lost={r.LastTrampLost} calypso={r.Survival?.CalypsoOperable}";
        Console.WriteLine();
        Console.WriteLine(lineLt);
        Console.Error.WriteLine(lineLt);
        Console.Out.Flush();
        Console.Error.Flush();
        return okLt ? 0 : 1;
      }

      var legs = session.Biographies.ForFirm(session.Ids.Carrier).Any()
                 || session.Sim.State.World.TransportStats.TransitSampleCount > 0
                 || session.Milestones.Entries.Any(m =>
                   m.Detail.Contains("Calypso", StringComparison.OrdinalIgnoreCase)
                   && m.Kind is "escrow" or "known-responsive");
      var liveFreight = CaptainJobBoard.ListLiveFreight(
        session.Sim, session.Ids, session.Player.DefaultProfile, session.CurrentSystemId, take: 32);
      var market = session.ListMarket();
      var charters = session.ListCharters();
      var ok = traveled && hauled && remoteReject && legs;
      var line = ok
        ? $"PLAYTEST PASS — travel={traveled} haul={hauled} remote-reject={remoteReject}"
        : $"PLAYTEST FAIL — travel={traveled} haul={hauled} remote-reject={remoteReject} legs={legs}";
      Console.WriteLine();
      Console.WriteLine(line);
      Console.Error.WriteLine(line);
      Console.Out.Flush();
      Console.Error.Flush();
      return ok ? 0 : 1;
    }

    PrintStatus(session, banner: false);
    AnsiConsole.WriteLine();
    SpectreHeadlessReport.Write(AnsiConsole.Console, session.ToResult());
    return exitCode;
  }

  private static async Task WaitPause(CampaignRunner.LiveSession session)
  {
    var sw = Stopwatch.StartNew();
    while (!session.IsWaitingForCaptain && !session.IsComplete && sw.Elapsed < TimeSpan.FromMinutes(3))
    {
      await Task.Delay(50).ConfigureAwait(false);
    }
  }

  private static IEnumerable<string> PlaytestScript()
  {
    // Spot intel → reject remote accept → travel to load hub → accept → depart → refuse standby if any → finish.
    yield return "status";
    yield return "spot";
    yield return "accept-remote"; // must refuse — not at load dock
    yield return "travel-to-best";
    yield return "continue";
    // Arrival may take several days; keep flowing until AT-DOCK appears or horizon.
    yield return "wait-dock-spot";
    yield return "accept-at-dock";
    yield return "depart";
    yield return "continue";
    yield return "status";
    yield return "refuse";
    yield return "resume";
  }

  private static IEnumerable<string> LastTrampPlaytestScript()
  {
    // Autopilot SurvivalCaptain does the hauls; we only keep time flowing to horizon / win.
    yield return "status";
    yield return "resume";
  }

  private static IEnumerable<string> ReadInteractive()
  {
    Console.WriteLine();
    Console.WriteLine($"{CampaignWorld.PlayerMasterLabel} — text captain desk");
    Console.WriteLine("See spot anywhere; accept only at load dock; travel empty. Type help.");
    while (true)
    {
      Console.Write("calypso> ");
      var line = Console.ReadLine();
      if (line is null) yield break;
      yield return line;
      if (line.Trim() is "quit" or "exit") yield break;
    }
  }

  private static bool IsFreeCommand(string line)
  {
    var cmd = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
    return cmd is "status" or "spot" or "jobs" or "charters" or "market" or "manifest" or "help" or "?"
      or "board" or "profile" or "step" or "continue" or "resume" or "quit" or "exit"
      or "save" or "saves" or "load"
      or "travel-to-best" or "accept-at-dock" or "accept-remote" or "wait-dock-spot"
      or "accept-charter" or "buy" or "sell";
  }

  private enum HandleResult { Ok, Advanced, Quit, Fail, WaitBerth }

  private static HandleResult Handle(
    string line,
    CampaignRunner.LiveSession session,
    CaptainDeskService desk,
    ref bool hauled,
    ref bool traveled,
    ref bool remoteReject)
  {
    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var cmd = parts[0].ToLowerInvariant();
    switch (cmd)
    {
      case "help":
      case "?":
        Console.WriteLine(
          """
          status / spot / charters / market / manifest
          accept N              Commit freight spot N if AT load dock (else refuse)
          accept-at-dock       First AT-DOCK freight into manifest
          accept-charter N      Commit goods charter N (or standby)
          buy N / sell N        Dock market ASK/BID into Calypso stock
          depart [sku]          Depart manifest lot
          travel <systemId>     Empty reposition
          travel-to-best        Travel to best remote freight origin
          refuse / wait / premium / overhaul
          board mesh|dock       Spot intel filter (aliases: network|berth|local)
          step / continue / resume / quit
          save [label] / saves / load latest|<guid>
          """);
        return HandleResult.Ok;

      case "status":
        PrintStatus(session, banner: false);
        return HandleResult.Ok;

      case "spot":
      case "jobs":
        PrintSpot(session);
        return HandleResult.Ok;

      case "charters":
        PrintCharters(session);
        return HandleResult.Ok;

      case "market":
        PrintMarket(session);
        return HandleResult.Ok;

      case "manifest":
        PrintManifest(session);
        return HandleResult.Ok;

      case "board":
        if (parts.Length >= 2)
        {
          session.Player.DockBoardOnly = parts[1].Equals("dock", StringComparison.OrdinalIgnoreCase)
                                          || parts[1].Equals("berth", StringComparison.OrdinalIgnoreCase)
                                          || parts[1].Equals("local", StringComparison.OrdinalIgnoreCase);
          if (parts[1].Equals("mesh", StringComparison.OrdinalIgnoreCase)
              || parts[1].Equals("network", StringComparison.OrdinalIgnoreCase)
              || parts[1].Equals("all", StringComparison.OrdinalIgnoreCase)
              || parts[1].Equals("global", StringComparison.OrdinalIgnoreCase))
          {
            session.Player.DockBoardOnly = false;
          }
        }

        Console.WriteLine($"board → {(session.Player.DockBoardOnly ? "dock" : "mesh")}");
        PrintSpot(session);
        return HandleResult.Ok;

      case "profile":
        if (parts.Length >= 2)
        {
          session.Player.DefaultProfile = parts[1].ToLowerInvariant() switch
          {
            "slow" => TransitProfile.SlowEconomic,
            "std" or "standard" => TransitProfile.StandardCommercial,
            "priority" or "pri" => TransitProfile.PriorityCommercial,
            _ => session.Player.DefaultProfile
          };
        }

        Console.WriteLine($"profile = {session.Player.DefaultProfile}");
        return HandleResult.Ok;

      case "accept":
      {
        var spots = session.ListJobs();
        if (spots.Count == 0)
        {
          Console.WriteLine("No spot postings");
          return HandleResult.Ok;
        }

        var idx = 0;
        if (parts.Length >= 2 && !int.TryParse(parts[1], out idx))
        {
          Console.WriteLine("accept N");
          return HandleResult.Fail;
        }

        if (idx < 0 || idx >= spots.Count)
        {
          Console.WriteLine("index out of range");
          return HandleResult.Fail;
        }

        var job = spots[idx];
        if (!job.AtOrigin)
        {
          remoteReject = true;
          Console.WriteLine($"REJECTED — not at load dock ({job.OriginName}). Travel first; posting may vanish.");
          return HandleResult.Ok;
        }

        return CommitAndContinue(session, desk, job, ref hauled);
      }

      case "accept-remote":
      {
        session.Player.DockBoardOnly = false;
        var job = session.ListJobs().FirstOrDefault(j => !j.AtOrigin);
        if (job is null)
        {
          Console.WriteLine("No remote spot to reject");
          remoteReject = true; // vacuously ok
          return HandleResult.Ok;
        }

        remoteReject = true;
        Console.WriteLine($"REJECTED — not at load dock ({job.OriginName}). Travel first.");
        return HandleResult.Ok;
      }

      case "accept-at-dock":
      case "accept-at-berth":
      {
        var job = CaptainJobBoard.ListLiveFreight(
            session.Sim, session.Ids, session.Player.DefaultProfile, session.CurrentSystemId, take: 16)
          .FirstOrDefault(j => j.AtOrigin);
        if (job is null)
        {
          Console.WriteLine("No AT-DOCK freight — try travel-to-best");
          return HandleResult.Ok;
        }

        return CommitAndContinue(session, desk, job, ref hauled);
      }

      case "depart":
      {
        var sku = parts.Length >= 2 ? parts[1] : null;
        return DeskAdvance(desk, new SessionCommandDto { ActionId = SessionActionIds.Depart, Sku = sku });
      }

      case "travel":
      {
        if (parts.Length < 2)
        {
          Console.WriteLine("travel <systemId>");
          return HandleResult.Ok;
        }

        traveled = true;
        return DeskAdvance(desk, new SessionCommandDto
        {
          ActionId = SessionActionIds.Travel,
          DestSystemId = parts[1],
        });
      }

      case "travel-to-best":
      {
        // Dock acts use live freight board — mesh digests can be empty early.
        var live = CaptainJobBoard.ListLiveFreight(
            session.Sim, session.Ids, session.Player.DefaultProfile, session.CurrentSystemId, take: 32)
          .OrderByDescending(j => j.Margin)
          .ToList();
        var remote = live.FirstOrDefault(j => !j.AtOrigin && j.Margin > 0m)
                     ?? live.FirstOrDefault(j => !j.AtOrigin);
        if (remote is null)
        {
          Console.WriteLine("No remote freight origin to travel to");
          return HandleResult.Ok;
        }

        traveled = true;
        Console.WriteLine($"(best) {remote.OriginName} for {remote.Label}");
        return DeskAdvance(desk, new SessionCommandDto
        {
          ActionId = SessionActionIds.Travel,
          DestSystemId = remote.OriginSystemId,
        });
      }

      case "wait-dock-spot":
        return HandleResult.WaitBerth;

      case "accept-charter":
      {
        var list = session.ListCharters();
        if (parts.Length < 2 || !int.TryParse(parts[1], out var ci) || ci < 0 || ci >= list.Count)
        {
          Console.WriteLine("accept-charter N");
          PrintCharters(session);
          return HandleResult.Ok;
        }

        hauled = true;
        return DeskAdvance(desk, new SessionCommandDto
        {
          ActionId = SessionActionIds.AcceptCharter,
          Index = ci,
        });
      }

      case "buy":
      case "sell":
      {
        var market = session.ListMarket();
        if (parts.Length < 2 || !int.TryParse(parts[1], out var mi) || mi < 0 || mi >= market.Count)
        {
          Console.WriteLine($"{cmd} N");
          PrintMarket(session);
          return HandleResult.Ok;
        }

        return DeskAdvance(desk, new SessionCommandDto
        {
          ActionId = cmd == "buy" ? SessionActionIds.MarketBuy : SessionActionIds.MarketSell,
          Index = mi,
        });
      }

      case "wait":
        return DeskAdvance(desk, new SessionCommandDto { ActionId = SessionActionIds.Wait });

      case "refuse":
        return DeskAdvance(desk, new SessionCommandDto { ActionId = SessionActionIds.RefuseStandby });

      case "premium":
        return DeskAdvance(desk, new SessionCommandDto { ActionId = SessionActionIds.Premium });

      case "overhaul":
        return DeskAdvance(desk, new SessionCommandDto { ActionId = SessionActionIds.Overhaul });

      case "step":
        return DeskAdvance(desk, new SessionCommandDto { ActionId = SessionActionIds.Step });

      case "continue":
      case "go":
        return DeskAdvance(desk, new SessionCommandDto { ActionId = SessionActionIds.Continue });

      case "resume":
        desk.Execute(new SessionCommandDto { ActionId = SessionActionIds.Resume });
        return HandleResult.Quit;

      case "save":
      {
        var label = parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : null;
        var saveResult = desk.Execute(new SessionCommandDto { ActionId = SessionActionIds.Save, Label = label });
        Console.WriteLine(saveResult.Ok
          ? $"{saveResult.Message} → {CampaignSaveStore.Default.RootPath}"
          : $"Save failed: {saveResult.Message}");
        return saveResult.Ok ? HandleResult.Ok : HandleResult.Fail;
      }

      case "saves":
        foreach (var s in CampaignSaveStore.Default.List().Take(12))
        {
          Console.WriteLine($"{s.Id:N}  d{s.DayIndex}  {s.SavedUtc:u}  {s.Label}  {s.SurvivalLine}");
        }

        return HandleResult.Ok;

      case "load":
        Console.WriteLine("Resume only at launch: --load latest|<guid>");
        return HandleResult.Ok;

      case "quit":
      case "exit":
        return HandleResult.Quit;

      default:
        Console.WriteLine($"Unknown '{cmd}' — help");
        return HandleResult.Ok;
    }
  }

  private static HandleResult DeskAdvance(CaptainDeskService desk, SessionCommandDto command)
  {
    var result = desk.Execute(command);
    Console.WriteLine(result.Ok
      ? result.Message
      : $"FAIL [{result.ErrorCode ?? "error"}] {result.Message}");
    return result.Ok ? HandleResult.Advanced : HandleResult.Ok;
  }

  private static HandleResult CommitAndContinue(
    CampaignRunner.LiveSession session,
    CaptainDeskService desk,
    CaptainJobBoard.SpotCandidate job,
    ref bool hauled)
  {
    var spots = session.ListJobs();
    var idx = spots.ToList().FindIndex(j =>
      j.OriginSystemId == job.OriginSystemId
      && j.DestSystemId == job.DestSystemId
      && j.SkuLabel == job.SkuLabel
      && j.Quantity == job.Quantity);
    if (idx < 0) idx = 0;
    hauled = true;
    return DeskAdvance(desk, new SessionCommandDto { ActionId = SessionActionIds.AcceptSpot, Index = idx });
  }

  private static void PrintStatus(CampaignRunner.LiveSession session, bool banner)
  {
    var desk = session.LastDesk ?? session.CaptureDesk();
    if (banner)
    {
      Console.WriteLine();
      Console.WriteLine("── DECISION ──────────────────────────────────────────");
    }

    Console.WriteLine($"d{desk.Day}  {desk.VoyageLine}");
    Console.WriteLine($"  cash {desk.CashLine}  {desk.StandingLine}  {desk.HoldLine}");
    Console.WriteLine($"  decision: {desk.DecisionLine}");
    if (desk.LastAction is { } last)
    {
      var tag = last.Ok ? "ok" : (last.ErrorCode ?? "fail");
      Console.WriteLine($"  last-action: [{tag}] {last.ActionId} — {last.Message}");
    }

    Console.WriteLine($"  {desk.MeshLine}");
    if (!string.IsNullOrEmpty(desk.CoachLine)) Console.WriteLine($"  {desk.CoachLine}");
    if (!string.IsNullOrEmpty(desk.SurvivalLine)) Console.WriteLine($"  {desk.SurvivalLine}");
    if (desk.StandbyOffer) Console.WriteLine("  STANDBY — refuse | accept standby via charters");
    if (!string.IsNullOrEmpty(desk.SoftFailLine)) Console.WriteLine($"  {desk.SoftFailLine}");
    if (banner)
    {
      PrintSpot(session);
      PrintManifest(session);
      Console.WriteLine("─────────────────────────────────────────────────────");
    }
  }

  private static void PrintSpot(CampaignRunner.LiveSession session)
  {
    var spots = session.ListJobs();
    Console.WriteLine($"  [SPOT {(session.Player.DockBoardOnly ? "dock" : "mesh")}] {spots.Count}  (take = AT DOCK only)");
    for (var i = 0; i < spots.Count; i++)
    {
      var j = spots[i];
      var tag = j.AtOrigin ? "TAKE" : "INTEL";
      Console.WriteLine(
        $"    [{i}] {tag,-5} {j.Label}  pay {j.ContractPay:0}  lift {j.LiftCost:0}  Δ{j.Margin:0.#}  ×{j.Quantity:0}");
    }
  }

  private static void PrintCharters(CampaignRunner.LiveSession session)
  {
    var list = session.ListCharters();
    Console.WriteLine($"  [GOODS CHARTERS] {list.Count}  (firm escrow A→B · local dock only)");
    for (var i = 0; i < list.Count; i++)
    {
      var c = list[i];
      if (c.ContractPay > 0m)
      {
        Console.WriteLine(
          $"    [{i}] {(c.CanAcceptHere ? "TAKE" : "HOLD")} {c.Label}  pay {c.ContractPay:0}  lift {c.LiftCost:0}  Δ{c.Margin:0.#}");
      }
      else
      {
        Console.WriteLine($"    [{i}] [{c.Kind}] {c.Label} — {c.Detail}");
      }
    }
  }

  private static void PrintMarket(CampaignRunner.LiveSession session)
  {
    var list = session.ListMarket();
    Console.WriteLine($"  [MARKET @ {session.CurrentSystemId}] {list.Count}");
    for (var i = 0; i < list.Count; i++)
    {
      Console.WriteLine($"    [{i}] {list[i].Summary}");
    }
  }

  private static void PrintManifest(CampaignRunner.LiveSession session)
  {
    var m = session.Player.Manifest;
    Console.WriteLine($"  [MANIFEST] {m.Used:0}/{CampaignWorld.HullCargoCapacity:0}");
    foreach (var lot in m.Lots)
    {
      Console.WriteLine($"    {lot.SkuLabel} → {lot.DestSystemId} ×{lot.Quantity:0} [{lot.Profile}]");
    }
  }
}
