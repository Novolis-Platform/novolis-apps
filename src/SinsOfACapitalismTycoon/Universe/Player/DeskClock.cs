namespace SinsOfACapitalismTycoon.Universe;

/// <summary>How the desk treats dock decisions relative to the sim clock.</summary>
internal enum DecisionAttention
{
  /// <summary>Never hard-pause for decisions; time flows at <see cref="PlayerControlState.SimSpeedScale"/>.</summary>
  RunAlways = 0,

  /// <summary>While a decision is needed, multiply pace delay (≈0.1× speed) instead of blocking.</summary>
  SoftSlow = 1,

  /// <summary>Hard gate until Continue / Step (legacy UntilDecision behaviour).</summary>
  HardPause = 2,
}

/// <summary>Maps 0..1 speed scale to wall-clock delay per simulated hour.</summary>
internal static class DeskClock
{
  /// <summary>Crawl: 1 real minute ≈ 1 game hour.</summary>
  public const double CrawlMsPerGameHour = 60_000;

  /// <summary>SoftSlow multiplies crawl delay while NeedsPlayerDecision.</summary>
  public const double SoftSlowFactor = 10.0;

  /// <summary>
  /// Wall milliseconds to wait after simulating <paramref name="hours"/> game hours.
  /// Scale 0 → crawl, 1 → no delay (max CPU).
  /// </summary>
  public static int DelayMs(double simSpeedScale, int hours, bool softSlowActive)
  {
    if (hours <= 0)
    {
      return 0;
    }

    var scale = Math.Clamp(simSpeedScale, 0.0, 1.0);
    var msPerHour = (1.0 - scale) * CrawlMsPerGameHour;
    if (softSlowActive)
    {
      msPerHour *= SoftSlowFactor;
    }

    var total = msPerHour * hours;
    if (total < 1.0)
    {
      return 0;
    }

    return total >= int.MaxValue ? int.MaxValue : (int)total;
  }

  public static string FormatAttention(DecisionAttention a) => a switch
  {
    DecisionAttention.RunAlways => "runAlways",
    DecisionAttention.SoftSlow => "softSlow",
    DecisionAttention.HardPause => "hardPause",
    _ => "runAlways",
  };

  /// <summary>Human line: game hours (and days) advanced per real minute.</summary>
  public static string FormatPace(double gameHoursPerRealMinute)
  {
    if (gameHoursPerRealMinute < 0.05)
    {
      return "pace stalled / paused";
    }

    var daysPerMin = gameHoursPerRealMinute / 24.0;
    if (daysPerMin >= 1.0)
    {
      return $"≈ {daysPerMin:0.#} game d / real min ({gameHoursPerRealMinute:0} h/min)";
    }

    if (gameHoursPerRealMinute >= 1.0)
    {
      return $"≈ {gameHoursPerRealMinute:0.#} game h / real min ({daysPerMin:0.##} d/min)";
    }

    var minPerGameHour = 1.0 / gameHoursPerRealMinute;
    return $"≈ {minPerGameHour:0.#} real min / game h ({daysPerMin:0.####} d/min)";
  }

  public static bool TryParseAttention(string? raw, out DecisionAttention attention)
  {
    attention = DecisionAttention.RunAlways;
    if (string.IsNullOrWhiteSpace(raw))
    {
      return false;
    }

    switch (raw.Trim().ToLowerInvariant())
    {
      case "run" or "runalways" or "always" or "off" or "none":
        attention = DecisionAttention.RunAlways;
        return true;
      case "soft" or "softslow" or "slow":
        attention = DecisionAttention.SoftSlow;
        return true;
      case "hard" or "hardpause" or "pause" or "untildecision":
        attention = DecisionAttention.HardPause;
        return true;
      default:
        return Enum.TryParse(raw, ignoreCase: true, out attention);
    }
  }
}
