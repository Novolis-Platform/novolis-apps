using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Novolis.Avalonia.Agent;
using Novolis.Avalonia.Agent.Protocol;
using Novolis.Avalonia.Briefing;
using Novolis.Avalonia.StarMap;
using Novolis.Avalonia.Studio;
using Novolis.Economy.Logistics;
using SinsOfACapitalismTycoon.Cli;
using SinsOfACapitalismTycoon.Universe;

namespace SinsOfACapitalismTycoon.Ui;

/// <summary>Captain’s desk: voyage, travel, spot/charter intel, berth manifest.</summary>
internal sealed class MainWindow : Window
{
  static readonly IBrush BrandBrush = new SolidColorBrush(Color.Parse("#d4a017"));
  static readonly IBrush MutedBrush = new SolidColorBrush(Color.Parse("#9a9aaa"));
  static readonly IBrush PanelBg = new SolidColorBrush(Color.Parse("#12141c"));

  readonly RunOptions _options;
  readonly StudioChrome _chrome;
  readonly StudioFeedback _feedback;
  readonly StarMapControl _map;
  readonly TextBlock _hubDetail;
  readonly TextBlock _subtitle;
  readonly TextBlock _voyage;
  readonly TextBlock _hullStats;
  readonly TextBlock _decision;
  readonly TextBlock _softFail;
  readonly TextBlock _survival;
  readonly ListBox _spot;
  readonly ListBox _charters;
  readonly ListBox _manifest;
  readonly ComboBox _profile;
  readonly ComboBox _boardScope;
  readonly FeedPanel _feed;
  readonly ScorecardView _scorecard;
  readonly DualMetricStrip _ledgers;
  readonly MetricTableView _registry;
  readonly MetricTableView _money;
  readonly MetricTableView _agents;
  readonly TextBox _raw;
  readonly TabControl _intelTabs;
  readonly Button _btnStep;
  readonly Button _btnContinue;
  readonly Button _btnResume;
  readonly Button _btnPause;
  readonly Button _btnTravel;
  readonly Button _btnSave;
  readonly Button _btnAcceptSpot;
  readonly Button _btnDepart;
  readonly Button _btnRefuseStandby;
  readonly Button _btnWait;
  readonly Button _btnPremium;
  readonly Button _btnOverhaul;
  readonly Button _btnAcceptStandby;
  readonly TextBox _travelSystem;

  CampaignRunner.LiveSession? _session;
  CaptainDeskModel? _desk;
  CampaignBriefingModel? _briefing;
  string? _mapSelection;

  public MainWindow(RunOptions options)
  {
    _options = options;
    Title = "Sins — Captain Desk · ST Calypso";
    Width = 1420;
    Height = 880;
    MinWidth = 980;
    MinHeight = 620;
    Background = new SolidColorBrush(Color.Parse("#0b1020"));

    _chrome = StudioChrome.Create();
    _feedback = _chrome.CreateFeedback();

    var brand = new TextBlock { Text = "Calypso", FontSize = 28, FontWeight = FontWeight.Bold, Foreground = BrandBrush };
    AgentProperties.SetId(brand, "calypso.brand");
    var title = new TextBlock
    {
      Text = "Captain Desk",
      FontSize = 18,
      FontWeight = FontWeight.SemiBold,
      VerticalAlignment = VerticalAlignment.Bottom,
      Margin = new Thickness(10, 0, 0, 2),
      Foreground = new SolidColorBrush(Color.Parse("#e8e8e8")),
    };
    AgentProperties.SetId(title, "calypso.title");
    _subtitle = new TextBlock
    {
      Text = CampaignWorld.PlayerMasterLabel,
      Foreground = MutedBrush,
      FontSize = 12,
      Margin = new Thickness(0, 4, 0, 0),
    };
    AgentProperties.SetId(_subtitle, "calypso.subtitle");

    _btnStep = TransportBtn("Step 1d", "calypso.step");
    _btnContinue = TransportBtn("Continue", "calypso.continue");
    _btnResume = TransportBtn("To horizon", "calypso.resume");
    _btnPause = TransportBtn("Pause next day", "calypso.pause");
    _btnTravel = TransportBtn("Travel here", "calypso.travel");
    _btnSave = TransportBtn("Save", "calypso.save");
    _btnStep.Click += (_, _) => _session?.StepDay();
    _btnContinue.Click += (_, _) => { _session?.Continue(); _feedback.SetStatus("Running until next decision…"); };
    _btnResume.Click += (_, _) => { _session?.ResumeToHorizon(); _feedback.SetStatus("Running to horizon…"); };
    _btnPause.Click += (_, _) => { _session?.Pause(); _feedback.SetStatus("Will pause after current day"); };
    _btnTravel.Click += (_, _) => TravelToSelection();
    _btnSave.Click += (_, _) => _ = SaveCheckpointAsync();

    _travelSystem = new TextBox
    {
      PlaceholderText = "system id (agent / typed travel)",
      Width = 200,
      FontSize = 12,
    };
    AgentProperties.SetId(_travelSystem, "calypso.travelSystem", AgentRoleNames.TextBox);
    _travelSystem.TextChanged += (_, _) => UpdateTravelEnabled();

    var transport = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 8,
      Margin = new Thickness(0, 8, 0, 0),
      Children = { _btnStep, _btnContinue, _btnResume, _btnPause, _travelSystem, _btnTravel, _btnSave },
    };

    var header = new StackPanel
    {
      Margin = new Thickness(16, 12, 16, 8),
      Children =
      {
        new StackPanel { Orientation = Orientation.Horizontal, Children = { brand, title } },
        _subtitle,
        _chrome.StatusLine,
        _chrome.FlashLine,
        transport,
      },
    };

    _map = new StarMapControl { MinHeight = 260 };
    AgentProperties.SetId(_map, "calypso.map");
    _map.StarSelected += OnStarSelected;
    _hubDetail = new TextBlock
    {
      Text = "Select a hub → Travel here (when docked idle).",
      Foreground = MutedBrush,
      TextWrapping = TextWrapping.Wrap,
      Margin = new Thickness(0, 8, 0, 0),
      FontSize = 12,
    };
    AgentProperties.SetId(_hubDetail, "calypso.hubDetail");

    var mapTitle = new TextBlock
    {
      Text = "Near-Sol · select destination to travel",
      FontWeight = FontWeight.SemiBold,
      Foreground = BrandBrush,
      Margin = new Thickness(0, 0, 0, 6),
    };
    var mapDock = new DockPanel { LastChildFill = true };
    DockPanel.SetDock(mapTitle, Dock.Top);
    DockPanel.SetDock(_hubDetail, Dock.Bottom);
    mapDock.Children.Add(mapTitle);
    mapDock.Children.Add(_hubDetail);
    mapDock.Children.Add(_map);

    var mapPanel = new Border
    {
      Background = PanelBg,
      Padding = new Thickness(8),
      CornerRadius = new CornerRadius(4),
      Child = mapDock,
    };

    _voyage = new TextBlock { Foreground = BrandBrush, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, FontSize = 14 };
    _hullStats = new TextBlock { Foreground = new SolidColorBrush(Color.Parse("#e8e8e8")), TextWrapping = TextWrapping.Wrap, FontSize = 13 };
    _decision = new TextBlock { Foreground = MutedBrush, TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 4, 0, 0) };
    _softFail = new TextBlock { Foreground = new SolidColorBrush(Color.Parse("#e07070")), TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    _survival = new TextBlock
    {
      Foreground = new SolidColorBrush(Color.Parse("#6ecf8e")),
      TextWrapping = TextWrapping.Wrap,
      FontSize = 12,
      FontWeight = FontWeight.SemiBold,
      Margin = new Thickness(0, 2, 0, 0),
    };
    AgentProperties.SetId(_voyage, "calypso.voyage");
    AgentProperties.SetId(_hullStats, "calypso.hull");
    AgentProperties.SetId(_decision, "calypso.decision");
    AgentProperties.SetId(_softFail, "calypso.softFail");
    AgentProperties.SetId(_survival, "calypso.survival");

    _profile = new ComboBox
    {
      Width = 180,
      ItemsSource = new[] { "SlowEconomic", "StandardCommercial", "PriorityCommercial" },
      SelectedIndex = 1,
    };
    AgentProperties.SetId(_profile, "calypso.profile", AgentRoleNames.ComboBox);
    _profile.SelectionChanged += (_, _) =>
    {
      if (_session is null || _profile.SelectedItem is not string name) return;
      if (!Enum.TryParse<TransitProfile>(name, out var p)) return;
      _session.Player.DefaultProfile = p;
      _session.Player.Orders.Enqueue(new PlayerOrder(PlayerOrderKind.SetDefaultProfile, Profile: p));
      _feedback.Flash($"Profile → {p}");
    };

    _boardScope = new ComboBox
    {
      Width = 120,
      ItemsSource = new[] { "Network", "Berth" },
      SelectedIndex = _options.Board == JobBoardScope.Local ? 1 : 0,
    };
    AgentProperties.SetId(_boardScope, "calypso.boardScope", AgentRoleNames.ComboBox);
    _boardScope.SelectionChanged += (_, _) =>
    {
      if (_session is null || _boardScope.SelectedItem is not string name) return;
      _session.Player.LocalBoardOnly = name.Equals("Berth", StringComparison.OrdinalIgnoreCase);
      RefreshDesk();
    };

    _btnAcceptSpot = TransportBtn("Accept at berth", "calypso.acceptSpot");
    _btnDepart = TransportBtn("Depart manifest", "calypso.depart");
    _btnRefuseStandby = TransportBtn("Refuse standby", "calypso.refuseStandby");
    _btnAcceptStandby = TransportBtn("Accept standby", "calypso.acceptStandby");
    _btnWait = TransportBtn("Wait", "calypso.wait");
    _btnPremium = TransportBtn("Pay premium", "calypso.premium");
    _btnOverhaul = TransportBtn("Request overhaul", "calypso.overhaul");
    _btnAcceptSpot.Click += (_, _) => AcceptSelectedSpot();
    _btnDepart.Click += (_, _) =>
    {
      Enqueue(new PlayerOrder(PlayerOrderKind.DepartManifest));
      _feedback.Flash("Depart queued");
    };
    _btnRefuseStandby.Click += (_, _) => Enqueue(new PlayerOrder(PlayerOrderKind.RefuseStandby));
    _btnAcceptStandby.Click += (_, _) => Enqueue(new PlayerOrder(PlayerOrderKind.AcceptStandby));
    _btnWait.Click += (_, _) => Enqueue(new PlayerOrder(PlayerOrderKind.Wait));
    _btnPremium.Click += (_, _) => Enqueue(new PlayerOrder(PlayerOrderKind.PayPremium));
    _btnOverhaul.Click += (_, _) => Enqueue(new PlayerOrder(PlayerOrderKind.RequestOverhaul));

    _spot = new ListBox { MinHeight = 120, MaxHeight = 200 };
    _charters = new ListBox { MinHeight = 80, MaxHeight = 140 };
    _manifest = new ListBox { MinHeight = 60, MaxHeight = 120 };
    AgentProperties.SetId(_spot, "calypso.spot", AgentRoleNames.ListBox);
    AgentProperties.SetId(_charters, "calypso.charters", AgentRoleNames.ListBox);
    AgentProperties.SetId(_manifest, "calypso.manifest", AgentRoleNames.ListBox);

    _intelTabs = new TabControl();
    AgentProperties.SetId(_intelTabs, "calypso.boards", AgentRoleNames.TabControl);
    _intelTabs.Items.Add(MakeTab("Spot intel", new StackPanel
    {
      Spacing = 8,
      Children =
      {
        new StackPanel
        {
          Orientation = Orientation.Horizontal,
          Spacing = 8,
          Children =
          {
            new TextBlock { Text = "Filter", VerticalAlignment = VerticalAlignment.Center, Foreground = MutedBrush, FontSize = 11 },
            _boardScope,
          },
        },
        _spot,
        _btnAcceptSpot,
      },
    }));
    _intelTabs.Items.Add(MakeTab("Charters", new StackPanel
    {
      Spacing = 8,
      Children = { _charters, new WrapPanel { Children = { _btnAcceptStandby, _btnRefuseStandby } } },
    }));
    _intelTabs.Items.Add(MakeTab("Manifest", new StackPanel
    {
      Spacing = 8,
      Children = { _manifest, _btnDepart },
    }));

    var voyagePanel = Section("Voyage", new StackPanel
    {
      Spacing = 6,
      Children =
      {
        _voyage,
        _hullStats,
        _decision,
        _survival,
        _softFail,
        new StackPanel
        {
          Orientation = Orientation.Horizontal,
          Spacing = 8,
          Children =
          {
            new TextBlock { Text = "Profile", VerticalAlignment = VerticalAlignment.Center, Foreground = MutedBrush },
            _profile,
          },
        },
        new WrapPanel { Children = { _btnPremium, _btnOverhaul, _btnWait } },
      },
    });

    _feed = new FeedPanel { MinHeight = 120 };
    _scorecard = new ScorecardView();
    _ledgers = new DualMetricStrip();
    _ledgers.SetPair("Calypso", "…", "Ops", "…", "Owner-master vs system liquid");
    _registry = new MetricTableView();
    _money = new MetricTableView();
    _agents = new MetricTableView();
    _raw = new TextBox
    {
      IsReadOnly = true,
      AcceptsReturn = true,
      TextWrapping = TextWrapping.NoWrap,
      FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New, monospace"),
      FontSize = 12,
    };

    var ledgerTabs = new TabControl();
    ledgerTabs.Items.Add(MakeTab("Registry", _registry));
    ledgerTabs.Items.Add(MakeTab("Money", WrapScroll(_money)));
    ledgerTabs.Items.Add(MakeTab("Agents", WrapScroll(_agents)));
    ledgerTabs.Items.Add(MakeTab("Raw", _raw));

    var rightScroll = new ScrollViewer
    {
      HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
      Content = new StackPanel
      {
        Spacing = 12,
        Margin = new Thickness(0, 0, 4, 0),
        Children =
        {
          voyagePanel,
          Section("Boards", _intelTabs),
          Section("Radio", _feed),
          Section("Life moments", _scorecard),
          Section("Ledgers", _ledgers),
          ledgerTabs,
        },
      },
    };

    var body = new Grid
    {
      ColumnDefinitions = new ColumnDefinitions("1.15*,*"),
      ColumnSpacing = 12,
      Margin = new Thickness(16, 0, 16, 8),
      Children = { mapPanel, rightScroll },
    };
    Grid.SetColumn(rightScroll, 1);

    Content = new Grid
    {
      RowDefinitions = new RowDefinitions("Auto,*,Auto"),
      Children = { header, body, _chrome.BusyOverlay },
    };
    Grid.SetRow(body, 1);
    Grid.SetRow(_chrome.BusyOverlay, 0);
    Grid.SetRowSpan(_chrome.BusyOverlay, 3);

    Opened += (_, _) => StartSession();
  }

  void StartSession()
  {
    _feedback.SetBusy(string.IsNullOrWhiteSpace(_options.LoadSave)
      ? "Seeding Near-Sol…"
      : "Loading checkpoint…");
    try
    {
      _ = Task.Run(async () =>
      {
        try
        {
          CampaignRunner.LiveSession session;
          if (!string.IsNullOrWhiteSpace(_options.LoadSave))
          {
            var save = await CampaignSaveResolver.ResolveAsync(_options.LoadSave).ConfigureAwait(false)
                       ?? throw new InvalidOperationException($"Save not found: {_options.LoadSave}");
            session = await CampaignRunner.LiveSession.FromSaveAsync(save).ConfigureAwait(false);
          }
          else
          {
            session = new CampaignRunner.LiveSession(
              _options.Seed,
              _options.DaysHours,
              _options.Drama,
              playerControl: _options.Player,
              autopilot: _options.Autopilot,
              localBoard: _options.Board == JobBoardScope.Local,
              lastTramp: _options.LastTramp);
          }

          _session = session;
          session.PauseMode = _options.Player ? CaptainPauseMode.UntilDecision : CaptainPauseMode.Never;
          session.DayEnded += () => Dispatcher.UIThread.Post(RefreshDesk);
          session.AwaitingDecision += () => Dispatcher.UIThread.Post(() =>
          {
            RefreshDesk();
            _feedback.ClearBusy();
            _feedback.Flash("Decision — dock act or travel");
            _feedback.SetStatus("Paused · Accept only at load berth · Travel via map");
          });

          Dispatcher.UIThread.Post(() =>
          {
            RefreshDesk();
            _feedback.ClearBusy();
            _feedback.SetStatus(_options.Player
              ? "See intel anywhere · accept only at berth · travel empty · Save checkpoints"
              : "Spectator run…");
          });

          await session.RunAsync(quiet: true, story: false).ConfigureAwait(false);
          try
          {
            await session.SaveCheckpointAsync("auto-horizon").ConfigureAwait(false);
          }
          catch
          {
            // non-fatal
          }

          Dispatcher.UIThread.Post(() =>
          {
            var briefing = CampaignBriefingModel.From(session.ToResult());
            _briefing = briefing;
            _raw.Text = briefing.RawReport;
            _feedback.ClearBusy();
            _feedback.Flash($"Horizon complete — {briefing.LifeMomentHits} life moments");
            SetTransportEnabled(false);
          });
        }
        catch (Exception ex)
        {
          CrashGuard.Report(ex, "MainWindow.RunAsync", openEditor: true, writeMiniDump: false);
          Dispatcher.UIThread.Post(() =>
          {
            _feedback.ClearBusy();
            _feedback.FlashError(ex.Message);
          });
        }
      });
    }
    catch (Exception ex)
    {
      CrashGuard.Report(ex, "MainWindow.StartCampaign", openEditor: true, writeMiniDump: false);
      _feedback.ClearBusy();
      _feedback.FlashError(ex.Message);
    }
  }

  async Task SaveCheckpointAsync()
  {
    if (_session is null)
    {
      return;
    }

    try
    {
      var record = await _session.SaveCheckpointAsync().ConfigureAwait(true);
      _feedback.Flash($"Saved {record.Label} ({record.Id:N})");
      _feedback.SetStatus($"Checkpoint → {CampaignSaveStore.Default.RootPath}");
    }
    catch (Exception ex)
    {
      CrashGuard.Report(ex, "MainWindow.Save", openEditor: false, writeMiniDump: false);
      _feedback.FlashError(ex.Message);
    }
  }

  void RefreshDesk()
  {
    if (_session is null) return;
    var desk = CaptainDeskModel.From(_session);
    _desk = desk;
    _subtitle.Text = desk.SubtitleLine;
    _voyage.Text = desk.VoyageLine;
    _hullStats.Text = $"Cash {desk.CashLine} · {desk.StandingLine}\n{desk.HullLine}\n{desk.HoldLine}";
    _decision.Text = desk.DecisionLine;
    _survival.Text = desk.SurvivalLine;
    _survival.IsVisible = !string.IsNullOrEmpty(desk.SurvivalLine);
    _survival.Foreground = desk.SurvivalLine.Contains("WIN", StringComparison.Ordinal)
      ? new SolidColorBrush(Color.Parse("#6ecf8e"))
      : desk.SurvivalLine.Contains("LOSE", StringComparison.Ordinal)
        ? new SolidColorBrush(Color.Parse("#e07070"))
        : new SolidColorBrush(Color.Parse("#9a9aaa"));
    _softFail.Text = desk.SoftFailLine;
    _softFail.IsVisible = !string.IsNullOrEmpty(desk.SoftFailLine);
    _map.SetMap(desk.MapPoints, desk.MapEdges);

    _spot.ItemsSource = desk.SpotJobs
      .Select(j => $"{(j.AtOrigin ? "●" : "○")} [{j.DistanceHint}] {j.Label}  Δ{j.Margin:0.#}  ×{j.Quantity:0}")
      .ToList();
    _charters.ItemsSource = desk.Charters
      .Select(c => $"[{c.Kind}] {c.Label} — {c.Detail}")
      .ToList();
    _manifest.ItemsSource = desk.ManifestLines.Count > 0
      ? desk.ManifestLines
      : new List<string> { "(empty — accept spot at berth)" };

    _feed.SetLines(desk.Feed);
    _scorecard.SetRows(desk.Scorecard, desk.ScorecardTitle);
    _ledgers.SetPair("Calypso", desk.CashLine, "Ops",
      desk.MoneyRows.FirstOrDefault(r => r.Key == "Ops liquid")?.Value ?? "—",
      "Never summed with Core");
    _registry.SetRows(desk.RegistryRows);
    _money.SetRows(desk.MoneyRows);
    _agents.SetRows(desk.AgentRows);

    _btnRefuseStandby.IsEnabled = desk.StandbyOffer;
    _btnAcceptStandby.IsEnabled = desk.StandbyOffer;
    UpdateTravelEnabled();
    _btnAcceptSpot.IsEnabled = desk.DockedIdle;
    _btnDepart.IsEnabled = desk.DockedIdle && desk.ManifestUsed > 0m;

    if (desk.SoftFail) _feedback.FlashError(desk.SoftFailLine);
    if (_session.IsWaitingForCaptain && !desk.Complete)
    {
      _feedback.ClearBusy();
      _feedback.SetStatus($"Day {desk.Day} — {desk.VoyageLine}");
    }
  }

  void AcceptSelectedSpot()
  {
    if (_session is null || _desk is null || _spot.SelectedIndex < 0 || _spot.SelectedIndex >= _desk.SpotJobs.Count)
    {
      _feedback.Flash("Select a spot posting");
      return;
    }

    var job = _desk.SpotJobs[_spot.SelectedIndex];
    if (!job.AtOrigin)
    {
      _feedback.FlashError($"Not at load berth — travel to {job.OriginName} first (intel can vanish meanwhile)");
      return;
    }

    _session.Player.Orders.Enqueue(new PlayerOrder(
      PlayerOrderKind.CommitSpot,
      OriginSystemId: job.OriginSystemId,
      DestSystemId: job.DestSystemId,
      SkuLabel: job.SkuLabel,
      Quantity: job.Quantity,
      LiftLimit: job.LiftLimit,
      DestBid: job.DestBid,
      Profile: job.Profile));
    _feedback.Flash($"Manifest + {job.Label}");
    _session.Continue();
  }

  void UpdateTravelEnabled()
  {
    var dest = ResolveTravelDest();
    _btnTravel.IsEnabled = _desk is { DockedIdle: true } && !string.IsNullOrEmpty(dest);
  }

  string? ResolveTravelDest()
  {
    var typed = _travelSystem.Text?.Trim();
    if (!string.IsNullOrEmpty(typed))
      return typed;
    return _mapSelection;
  }

  void TravelToSelection()
  {
    var dest = ResolveTravelDest();
    if (_session is null || string.IsNullOrEmpty(dest))
    {
      _feedback.Flash("Select a hub on the map or type a system id");
      return;
    }

    if (_desk is { DockedIdle: false })
    {
      _feedback.FlashError("Hull busy — wait for berth");
      return;
    }

    _session.Player.TravelTargetSystemId = dest;
    _session.Player.Orders.Enqueue(new PlayerOrder(
      PlayerOrderKind.TravelTo,
      DestSystemId: dest,
      Profile: _session.Player.DefaultProfile));
    _feedback.Flash($"Travel → {dest}");
    _session.Continue();
  }

  void Enqueue(PlayerOrder order)
  {
    if (_session is null) return;
    _session.Player.Orders.Enqueue(order);
    _session.Continue();
  }

  void SetTransportEnabled(bool on)
  {
    _btnStep.IsEnabled = on;
    _btnContinue.IsEnabled = on;
    _btnResume.IsEnabled = on;
    _btnPause.IsEnabled = on;
    _btnTravel.IsEnabled = on;
    _btnAcceptSpot.IsEnabled = on;
    _btnDepart.IsEnabled = on;
  }

  void OnStarSelected(string id)
  {
    _mapSelection = id;
    if (_session is not null)
    {
      _session.Player.TravelTargetSystemId = id;
    }

    if (string.IsNullOrWhiteSpace(_travelSystem.Text))
      _travelSystem.Text = id;

    if (_desk?.HubDetails.TryGetValue(id, out var hub) == true)
    {
      var at = string.Equals(id, _desk.CurrentHubSystemId, StringComparison.OrdinalIgnoreCase);
      _hubDetail.Text = at
        ? $"{hub.Name} · HERE (berth)\n{hub.ProfileHint}"
        : $"{hub.Name} · {hub.Role}\n{hub.ProfileHint}\n→ Travel here when idle";
      _hubDetail.Foreground = new SolidColorBrush(Color.Parse("#e8e8e8"));
    }
    else
    {
      _hubDetail.Text = id;
    }

    UpdateTravelEnabled();
  }

  static Button TransportBtn(string text, string agentId)
  {
    var btn = new Button { Content = text, Padding = new Thickness(12, 6), Margin = new Thickness(0, 0, 4, 4) };
    AgentProperties.SetId(btn, agentId, AgentRoleNames.Button);
    return btn;
  }

  static TabItem MakeTab(string header, Control content) =>
    new() { Header = header, Content = content };

  static Control WrapScroll(Control child) =>
    new ScrollViewer
    {
      HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
      Content = child,
    };

  static Control Section(string title, Control child) =>
    new Border
    {
      Background = PanelBg,
      Padding = new Thickness(10),
      CornerRadius = new CornerRadius(4),
      Child = new StackPanel
      {
        Spacing = 8,
        Children =
        {
          new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, Foreground = BrandBrush },
          child,
        },
      },
    };
}

/// <summary>Simple post-run text viewer for Core smoke engine.</summary>
internal sealed class CoreReportWindow : Window
{
  public CoreReportWindow(string reportText)
  {
    Title = "Sins — Core smoke";
    Width = 900;
    Height = 700;
    Content = new TextBox
    {
      Text = reportText,
      IsReadOnly = true,
      AcceptsReturn = true,
      TextWrapping = TextWrapping.NoWrap,
      FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New, monospace"),
      FontSize = 13,
      Margin = new Thickness(16),
    };
  }
}
