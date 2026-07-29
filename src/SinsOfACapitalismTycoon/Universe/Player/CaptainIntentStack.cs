namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Lifecycle of one captain intent / compound step.</summary>
internal enum IntentStepStatus
{
  Pending,
  Active,
  WaitingFuel,
  WaitingCargo,
  Blocked,
  Failed,
  Done,
}

/// <summary>One visible entry on the captain action stack.</summary>
internal sealed class IntentStep
{
  public required Guid Id { get; init; }
  public required string Label { get; init; }
  public IntentStepStatus Status { get; set; } = IntentStepStatus.Pending;
  public string? Detail { get; set; }
  public PlayerOrder? Order { get; init; }
  public bool IsCompound { get; init; }
}

/// <summary>
/// Per-actor (Calypso) action stack: ordered intents drained as atomic <see cref="PlayerOrder"/>s.
/// Compound recipes expand into named sub-steps; bunker/lift surface as Waiting* statuses.
/// </summary>
internal sealed class CaptainIntentStack
{
  private readonly object _gate = new();
  private readonly List<IntentStep> _steps = [];
  private Guid? _activeId;

  public int Count
  {
    get
    {
      lock (_gate)
      {
        return _steps.Count(s => s.Status is not IntentStepStatus.Done and not IntentStepStatus.Failed);
      }
    }
  }

  public bool IsBlocked
  {
    get
    {
      lock (_gate)
      {
        return _steps.Any(s =>
          s.Status is IntentStepStatus.Blocked or IntentStepStatus.WaitingFuel or IntentStepStatus.WaitingCargo);
      }
    }
  }

  public IReadOnlyList<IntentStep> Snapshot()
  {
    lock (_gate)
    {
      return _steps.Select(s => new IntentStep
      {
        Id = s.Id,
        Label = s.Label,
        Status = s.Status,
        Detail = s.Detail,
        Order = s.Order,
        IsCompound = s.IsCompound,
      }).ToList();
    }
  }

  public string[] FormatLines()
  {
    lock (_gate)
    {
      return _steps
        .Where(s => s.Status is not IntentStepStatus.Done)
        .Select(s => $"{StatusTag(s.Status)} {s.Label}" + (string.IsNullOrEmpty(s.Detail) ? "" : $" — {s.Detail}"))
        .ToArray();
    }
  }

  public IntentStep Push(PlayerOrder order, string? label = null)
  {
    var step = new IntentStep
    {
      Id = Guid.NewGuid(),
      Label = label ?? LabelFor(order),
      Status = IntentStepStatus.Pending,
      Order = order,
    };
    lock (_gate)
    {
      _steps.Add(step);
    }

    return step;
  }

  /// <summary>Premium (optional) then depart — multi-decision sequence. Returns orders to enqueue.</summary>
  public IReadOnlyList<PlayerOrder> PushPrepareAndDepart(bool includePremium, bool includeOverhaul, string? sku = null)
  {
    var orders = new List<PlayerOrder>();
    lock (_gate)
    {
      if (includePremium)
      {
        var o = new PlayerOrder(PlayerOrderKind.PayPremium);
        _steps.Add(new IntentStep
        {
          Id = Guid.NewGuid(),
          Label = "Pay premium",
          Status = IntentStepStatus.Pending,
          Order = o,
        });
        orders.Add(o);
      }

      if (includeOverhaul)
      {
        var o = new PlayerOrder(PlayerOrderKind.RequestOverhaul);
        _steps.Add(new IntentStep
        {
          Id = Guid.NewGuid(),
          Label = "Request overhaul",
          Status = IntentStepStatus.Pending,
          Order = o,
        });
        orders.Add(o);
      }

      var depart = new PlayerOrder(PlayerOrderKind.DepartManifest, SkuLabel: sku);
      _steps.Add(new IntentStep
      {
        Id = Guid.NewGuid(),
        Label = string.IsNullOrWhiteSpace(sku) ? "Depart manifest" : $"Depart {sku}",
        Status = IntentStepStatus.Pending,
        Order = depart,
        IsCompound = true,
      });
      orders.Add(depart);
    }

    return orders;
  }

  public void Clear()
  {
    lock (_gate)
    {
      _steps.Clear();
      _activeId = null;
    }
  }

  public void MarkActive(PlayerOrderKind kind)
  {
    lock (_gate)
    {
      var step = _steps.FirstOrDefault(s =>
        s.Status is IntentStepStatus.Pending or IntentStepStatus.WaitingFuel or IntentStepStatus.WaitingCargo
        && s.Order?.Kind == kind);
      step ??= _steps.LastOrDefault(s =>
        s.Status is IntentStepStatus.Pending or IntentStepStatus.Active
        && s.Order?.Kind == kind);
      if (step is null)
      {
        return;
      }

      step.Status = IntentStepStatus.Active;
      _activeId = step.Id;
    }
  }

  public void MarkWaitingFuel(string? detail = null) => SetActiveStatus(IntentStepStatus.WaitingFuel, detail);

  public void MarkWaitingCargo(string? detail = null) => SetActiveStatus(IntentStepStatus.WaitingCargo, detail);

  public void MarkDone(PlayerOrderKind kind)
  {
    lock (_gate)
    {
      var step = FindForKind(kind);
      if (step is null)
      {
        return;
      }

      step.Status = IntentStepStatus.Done;
      if (_activeId == step.Id)
      {
        _activeId = null;
      }

      PruneDone();
    }
  }

  public void MarkFailed(PlayerOrderKind kind, string? detail = null)
  {
    lock (_gate)
    {
      var step = FindForKind(kind);
      if (step is null)
      {
        return;
      }

      step.Status = IntentStepStatus.Failed;
      step.Detail = detail;
      if (_activeId == step.Id)
      {
        _activeId = null;
      }
    }
  }

  private void SetActiveStatus(IntentStepStatus status, string? detail)
  {
    lock (_gate)
    {
      IntentStep? step = null;
      if (_activeId is { } id)
      {
        step = _steps.FirstOrDefault(s => s.Id == id);
      }

      step ??= _steps.LastOrDefault(s =>
        s.Status is IntentStepStatus.Active or IntentStepStatus.Pending
          or IntentStepStatus.WaitingFuel or IntentStepStatus.WaitingCargo);
      if (step is null)
      {
        return;
      }

      step.Status = status;
      step.Detail = detail;
      _activeId = step.Id;
    }
  }

  private IntentStep? FindForKind(PlayerOrderKind kind) =>
    _steps.LastOrDefault(s =>
      s.Order?.Kind == kind
      && s.Status is not IntentStepStatus.Done and not IntentStepStatus.Failed);

  private void PruneDone()
  {
    while (_steps.Count > 0 && _steps[0].Status is IntentStepStatus.Done)
    {
      _steps.RemoveAt(0);
    }
  }

  private static string StatusTag(IntentStepStatus s) => s switch
  {
    IntentStepStatus.Pending => "[ ]",
    IntentStepStatus.Active => "[>]",
    IntentStepStatus.WaitingFuel => "[F]",
    IntentStepStatus.WaitingCargo => "[C]",
    IntentStepStatus.Blocked => "[!]",
    IntentStepStatus.Failed => "[x]",
    IntentStepStatus.Done => "[✓]",
    _ => "[?]",
  };

  private static string LabelFor(PlayerOrder order) => order.Kind switch
  {
    PlayerOrderKind.CommitSpot => $"Accept {order.SkuLabel ?? "spot"}",
    PlayerOrderKind.DepartManifest => string.IsNullOrWhiteSpace(order.SkuLabel)
      ? "Depart manifest"
      : $"Depart {order.SkuLabel}",
    PlayerOrderKind.TravelTo => $"Travel → {order.DestSystemId}",
    PlayerOrderKind.PayPremium => "Pay premium",
    PlayerOrderKind.RequestOverhaul => "Request overhaul",
    PlayerOrderKind.MarketBuy => $"Buy {order.SkuLabel}",
    PlayerOrderKind.MarketSell => $"Sell {order.SkuLabel}",
    PlayerOrderKind.RefuseStandby => "Refuse standby",
    PlayerOrderKind.AcceptStandby => "Accept standby",
    PlayerOrderKind.Wait => "Wait",
    PlayerOrderKind.SetDefaultProfile => "Set profile",
    _ => order.Kind.ToString(),
  };
}
