using System.Diagnostics;
using Novolis.Economy.Logistics;
using Spectre.Console;
using SinsOfACapitalismTycoon.Universe;

namespace SinsOfACapitalismTycoon.Cli;

/// <summary>
/// Text captain desk — scriptable. Verbs mirror Avalonia:
/// status, spot, charters, manifest, accept N, depart, travel &lt;system&gt;,
/// refuse, wait, premium, overhaul, board network|berth, step, continue, resume, help, quit.
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
        localBoard: options.Board == JobBoardScope.Local,
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

      var result = Handle(line, session, ref hauled, ref traveled, ref remoteReject);
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
          session.Player.LocalBoardOnly = false;
          if (session.ListJobs().Any(j => j.AtOrigin))
          {
            Console.WriteLine($"AT-BERTH spot ready @ {session.CurrentHubSystemId}");
            break;
          }

          Console.WriteLine(
            $"… waiting berth ({session.CurrentHubSystemId}) · {session.Agents.CarrierPulse.LastDecision}");
          session.Continue();
          await WaitPause(session).ConfigureAwait(false);
        }

        if (!session.ListJobs().Any(j => j.AtOrigin))
        {
          Console.WriteLine("Timed out waiting for AT-BERTH spot");
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
    yield return "accept-remote"; // must refuse — not at load berth
    yield return "travel-to-best";
    yield return "continue";
    // Arrival may take several days; keep flowing until AT-BERTH appears or horizon.
    yield return "wait-berth-spot";
    yield return "accept-at-berth";
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
    Console.WriteLine("See spot anywhere; accept only at load berth; travel empty. Type help.");
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
    return cmd is "status" or "spot" or "jobs" or "charters" or "manifest" or "help" or "?"
      or "board" or "profile" or "step" or "continue" or "resume" or "quit" or "exit"
      or "save" or "saves" or "load"
      or "travel-to-best" or "accept-at-berth" or "accept-remote" or "wait-berth-spot";
  }

  private enum HandleResult { Ok, Advanced, Quit, Fail, WaitBerth }

  private static HandleResult Handle(
    string line,
    CampaignRunner.LiveSession session,
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
          status / spot / charters / manifest
          accept N              Commit spot N if AT load berth (else refuse)
          accept-at-berth       First AT-BERTH spot into manifest
          depart [sku]          Depart manifest lot
          travel <systemId>     Empty reposition
          travel-to-best        Travel to best remote spot origin
          refuse / wait / premium / overhaul
          board network|berth   Spot intel filter
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

      case "manifest":
        PrintManifest(session);
        return HandleResult.Ok;

      case "board":
        if (parts.Length >= 2)
        {
          session.Player.LocalBoardOnly = parts[1].Equals("berth", StringComparison.OrdinalIgnoreCase)
                                          || parts[1].Equals("local", StringComparison.OrdinalIgnoreCase);
        }

        Console.WriteLine($"board → {(session.Player.LocalBoardOnly ? "berth" : "network")}");
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
          Console.WriteLine($"REJECTED — not at load berth ({job.OriginName}). Travel first; posting may vanish.");
          return HandleResult.Ok;
        }

        return CommitAndContinue(session, job, ref hauled);
      }

      case "accept-remote":
      {
        session.Player.LocalBoardOnly = false;
        var job = session.ListJobs().FirstOrDefault(j => !j.AtOrigin);
        if (job is null)
        {
          Console.WriteLine("No remote spot to reject");
          remoteReject = true; // vacuously ok
          return HandleResult.Ok;
        }

        remoteReject = true;
        Console.WriteLine($"REJECTED — not at load berth ({job.OriginName}). Travel first.");
        return HandleResult.Ok;
      }

      case "accept-at-berth":
      {
        var job = session.ListJobs().FirstOrDefault(j => j.AtOrigin);
        if (job is null)
        {
          // Force berth filter then network scan for AtOrigin after travel.
          session.Player.LocalBoardOnly = false;
          job = session.ListJobs().FirstOrDefault(j => j.AtOrigin);
        }

        if (job is null)
        {
          Console.WriteLine("No AT-BERTH spot — try travel-to-best");
          return HandleResult.Ok;
        }

        return CommitAndContinue(session, job, ref hauled);
      }

      case "depart":
      {
        var sku = parts.Length >= 2 ? parts[1] : null;
        session.Player.Orders.Enqueue(new PlayerOrder(PlayerOrderKind.DepartManifest, SkuLabel: sku));
        Console.WriteLine("Depart queued");
        session.Continue();
        return HandleResult.Advanced;
      }

      case "travel":
      {
        if (parts.Length < 2)
        {
          Console.WriteLine("travel <systemId>");
          return HandleResult.Ok;
        }

        session.Player.Orders.Enqueue(new PlayerOrder(
          PlayerOrderKind.TravelTo, DestSystemId: parts[1], Profile: session.Player.DefaultProfile));
        traveled = true;
        Console.WriteLine($"Travel → {parts[1]}");
        session.Continue();
        return HandleResult.Advanced;
      }

      case "travel-to-best":
      {
        session.Player.LocalBoardOnly = false;
        var remote = session.ListJobs().FirstOrDefault(j => !j.AtOrigin && j.Margin > 0m)
                     ?? session.ListJobs().FirstOrDefault(j => !j.AtOrigin);
        if (remote is null)
        {
          Console.WriteLine("No remote spot origin to travel to");
          return HandleResult.Ok;
        }

        session.Player.Orders.Enqueue(new PlayerOrder(
          PlayerOrderKind.TravelTo,
          DestSystemId: remote.OriginSystemId,
          Profile: session.Player.DefaultProfile));
        traveled = true;
        Console.WriteLine($"Travel → {remote.OriginSystemId} ({remote.OriginName}) for {remote.Label}");
        session.Continue();
        return HandleResult.Advanced;
      }

      case "wait-berth-spot":
        return HandleResult.WaitBerth;

      case "wait":
        session.Player.Orders.Enqueue(new PlayerOrder(PlayerOrderKind.Wait));
        session.Continue();
        return HandleResult.Advanced;

      case "refuse":
        if (session.Player.Opportunities?.ActiveStandbyTramp?.Equals(session.Ids.Carrier) == true)
        {
          session.Player.Orders.Enqueue(new PlayerOrder(PlayerOrderKind.RefuseStandby));
          Console.WriteLine("Refuse standby queued");
        }
        else
        {
          Console.WriteLine("No active standby");
        }

        session.Continue();
        return HandleResult.Advanced;

      case "premium":
        session.Player.Orders.Enqueue(new PlayerOrder(PlayerOrderKind.PayPremium));
        session.Continue();
        return HandleResult.Advanced;

      case "overhaul":
        session.Player.Orders.Enqueue(new PlayerOrder(PlayerOrderKind.RequestOverhaul));
        session.Continue();
        return HandleResult.Advanced;

      case "step":
        session.StepDay();
        return HandleResult.Advanced;

      case "continue":
      case "go":
        session.Continue();
        return HandleResult.Advanced;

      case "resume":
        session.ResumeToHorizon();
        return HandleResult.Quit;

      case "save":
      {
        try
        {
          var label = parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : null;
          var record = session.SaveCheckpointAsync(label).AsTask().GetAwaiter().GetResult();
          Console.WriteLine($"Saved {record.Label} id={record.Id:N} → {CampaignSaveStore.Default.RootPath}");
        }
        catch (Exception ex)
        {
          Console.WriteLine($"Save failed: {ex.Message}");
          return HandleResult.Fail;
        }

        return HandleResult.Ok;
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

  private static HandleResult CommitAndContinue(
    CampaignRunner.LiveSession session,
    CaptainJobBoard.SpotCandidate job,
    ref bool hauled)
  {
    session.Player.Orders.Enqueue(new PlayerOrder(
      PlayerOrderKind.CommitSpot,
      OriginSystemId: job.OriginSystemId,
      DestSystemId: job.DestSystemId,
      SkuLabel: job.SkuLabel,
      Quantity: job.Quantity,
      LiftLimit: job.LiftLimit,
      DestBid: job.DestBid,
      Profile: job.Profile));
    hauled = true;
    Console.WriteLine($"Manifest + {job.Label} Δ{job.Margin:0.#}");
    session.Continue();
    return HandleResult.Advanced;
  }

  private static void PrintStatus(CampaignRunner.LiveSession session, bool banner)
  {
    var desk = Ui.CaptainDeskModel.From(session);
    if (banner)
    {
      Console.WriteLine();
      Console.WriteLine("── DECISION ──────────────────────────────────────────");
    }

    Console.WriteLine($"d{desk.Day}  {desk.VoyageLine}");
    Console.WriteLine($"  cash {desk.CashLine}  {desk.StandingLine}  {desk.HoldLine}");
    Console.WriteLine($"  decision: {desk.DecisionLine}");
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
    Console.WriteLine($"  [SPOT {(session.Player.LocalBoardOnly ? "berth" : "network")}] {spots.Count}");
    for (var i = 0; i < spots.Count; i++)
    {
      var j = spots[i];
      Console.WriteLine(
        $"    [{i}] {j.DistanceHint,-12} {j.Label}  Δ{j.Margin:0.#}  ×{j.Quantity:0}");
    }
  }

  private static void PrintCharters(CampaignRunner.LiveSession session)
  {
    var list = session.ListCharters();
    Console.WriteLine($"  [CHARTERS] {list.Count}");
    foreach (var c in list)
    {
      Console.WriteLine($"    [{c.Kind}] {c.Label} — {c.Detail}");
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
