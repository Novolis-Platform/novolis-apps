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
using Novolis.Game.Session;
using SinsOfACapitalismTycoon.Cli;
using SinsOfACapitalismTycoon.Universe;

namespace SinsOfACapitalismTycoon.Ui;

/// <summary>Captain’s desk: voyage, travel, spot/charter intel, dock manifest.</summary>
internal sealed class MainWindow : Window
{
  readonly RunOptions _options;
  readonly StudioChrome _chrome;
  readonly StudioFeedback _feedback;
  readonly StarMapControl _map;
  readonly TextBlock _hubDetail;
  readonly TextBlock _subtitle;
  readonly TextBlock _voyage;
  readonly TextBlock _hullStats;
  readonly TextBlock _cashChipValue;
  readonly TextBlock _lifeChipValue;
  readonly TextBlock _decision;
  readonly TextBlock _coach;
  readonly TextBlock _softFail;
  readonly TextBlock _survival;
  readonly ListBox _spot;
  readonly ListBox _charters;
  readonly ListBox _market;
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
  readonly Button _btnAcceptCharter;
  readonly Button _btnDepart;
  readonly Button _btnRefuseStandby;
  readonly Button _btnWait;
  readonly Button _btnPremium;
  readonly Button _btnOverhaul;
  readonly Button _btnAcceptStandby;
  readonly Button _btnMarketBuy;
  readonly Button _btnMarketSell;
  readonly Button _btnCancelStack;
  readonly Button _btnPrepareDepart;
  readonly TextBox _travelSystem;
  readonly ComboBox _attention;
  readonly Slider _speed;
  readonly TextBlock _speedLabel;
  readonly ListBox _intentStack;
  readonly TextBlock _clockLine;
  readonly Expander _opsExpander;
  readonly Expander _papersExpander;
  DispatcherTimer? _flashClear;

  CampaignRunner.LiveSession? _session;
  CaptainDeskService? _deskService;
  SessionSurface? _sessionSurface;
  CaptainDeskModel? _desk;
  CampaignBriefingModel? _briefing;
  string? _mapSelection;
  string? _routeOrigin;
  string? _routeDest;
  string? _pendingTravelDest;
  string? _pendingTravelOrigin;
  bool _routePathWarned;
  bool _syncingClock;

  public MainWindow(RunOptions options)
  {
    _options = options;
    CalypsoTheme.ApplyWindowChrome(this);
    Title = "Sins — Captain Desk · ST Calypso";
    Width = 1420;
    Height = 880;
    MinWidth = 980;
    MinHeight = 620;

    _chrome = StudioChrome.Create();
    _feedback = _chrome.CreateFeedback();
    _chrome.FlashLine.FontFamily = CalypsoPalette.BodyFont;
    _chrome.StatusLine.FontFamily = CalypsoPalette.BodyFont;
    _chrome.StatusLine.Foreground = CalypsoPalette.MutedBrush;

    var brand = new TextBlock
    {
      Text = "Calypso",
      FontSize = 34,
      FontWeight = FontWeight.Bold,
      FontFamily = CalypsoPalette.DisplayFont,
      Foreground = CalypsoPalette.AccentBrush,
    };
    AgentProperties.SetId(brand, "calypso.brand");
    var title = new TextBlock
    {
      Text = "Captain Desk",
      FontSize = 16,
      FontWeight = FontWeight.SemiBold,
      FontFamily = CalypsoPalette.BodyFont,
      VerticalAlignment = VerticalAlignment.Bottom,
      Margin = new Thickness(12, 0, 0, 4),
      Foreground = CalypsoPalette.MutedBrush,
    };
    AgentProperties.SetId(title, "calypso.title");
    _subtitle = new TextBlock
    {
      Text = CampaignWorld.PlayerMasterLabel,
      Foreground = CalypsoPalette.MutedBrush,
      FontFamily = CalypsoPalette.BodyFont,
      FontSize = 12,
      Margin = new Thickness(0, 4, 0, 0),
    };
    AgentProperties.SetId(_subtitle, "calypso.subtitle");

    _btnStep = CalypsoTheme.MakeButton("Step 1d", "calypso.step", CalypsoButtonKind.Secondary);
    _btnContinue = CalypsoTheme.MakeButton("Continue", "calypso.continue", CalypsoButtonKind.Secondary);
    _btnResume = CalypsoTheme.MakeButton("To horizon", "calypso.resume", CalypsoButtonKind.Secondary);
    _btnPause = CalypsoTheme.MakeButton("Pause next day", "calypso.pause", CalypsoButtonKind.Quiet);
    _btnTravel = CalypsoTheme.MakeButton("Travel here", "calypso.travel", CalypsoButtonKind.Primary);
    _btnSave = CalypsoTheme.MakeButton("Save", "calypso.save", CalypsoButtonKind.Quiet);
    _btnCancelStack = CalypsoTheme.MakeButton("Cancel stack", "calypso.cancelStack", CalypsoButtonKind.Quiet);
    _btnPrepareDepart = CalypsoTheme.MakeButton("Prepare & depart", "calypso.prepareDepart", CalypsoButtonKind.Primary);
    _btnStep.Click += (_, _) => DeskExec(new SessionCommandDto { ActionId = SessionActionIds.Step });
    _btnContinue.Click += (_, _) =>
    {
      DeskExec(new SessionCommandDto { ActionId = SessionActionIds.Continue });
      _feedback.SetStatus("Running…");
    };
    _btnResume.Click += (_, _) =>
    {
      DeskExec(new SessionCommandDto { ActionId = SessionActionIds.Resume });
      _feedback.SetStatus("Running to horizon…");
    };
    _btnPause.Click += (_, _) => { _session?.Pause(); _feedback.SetStatus("Will pause after current day"); };
    _btnTravel.Click += (_, _) => TravelToSelection();
    _btnSave.Click += (_, _) => _ = SaveCheckpointAsync();
    _btnCancelStack.Click += (_, _) =>
      DeskExec(new SessionCommandDto { ActionId = SessionActionIds.CancelStack });
    _btnPrepareDepart.Click += (_, _) =>
      DeskExec(new SessionCommandDto { ActionId = SessionActionIds.PrepareDepart, Prepare = true });

    _attention = new ComboBox
    {
      Width = 130,
      ItemsSource = new[] { "Run always", "Soft slow", "Hard pause" },
      SelectedIndex = 2,
    };
    AgentProperties.SetId(_attention, "calypso.attention", AgentRoleNames.ComboBox);
    _attention.SelectionChanged += (_, _) => ApplyClockFromUi();

    _speed = new Slider
    {
      Width = 160,
      Minimum = 0,
      Maximum = 1,
      Value = 1,
      TickFrequency = 0.25,
      IsSnapToTickEnabled = false,
    };
    AgentProperties.SetId(_speed, "calypso.speed");
    _speedLabel = new TextBlock
    {
      Text = "Speed Max",
      Width = 88,
      VerticalAlignment = VerticalAlignment.Center,
      Foreground = CalypsoPalette.MutedBrush,
      FontSize = 11,
    };
    _speed.PropertyChanged += (_, e) =>
    {
      if (e.Property != Slider.ValueProperty) return;
      _speedLabel.Text = SpeedLabel(_speed.Value);
      ApplyClockFromUi();
    };

    _clockLine = new TextBlock
    {
      Text = "clock hardPause · speed 1",
      Foreground = CalypsoPalette.MutedBrush,
      FontSize = 11,
      VerticalAlignment = VerticalAlignment.Center,
    };
    AgentProperties.SetId(_clockLine, "calypso.clockLine");

    _intentStack = new ListBox { MinHeight = 48, MaxHeight = 100 };
    AgentProperties.SetId(_intentStack, "calypso.intentStack", AgentRoleNames.ListBox);

    _travelSystem = new TextBox
    {
      PlaceholderText = "system id (agent / typed travel)",
      Width = 200,
      FontSize = 12,
      FontFamily = CalypsoPalette.BodyFont,
    };
    AgentProperties.SetId(_travelSystem, "calypso.travelSystem", AgentRoleNames.TextBox);
    _travelSystem.TextChanged += (_, _) => UpdateTravelEnabled();

    var transport = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 4,
      Margin = new Thickness(0, 8, 0, 0),
      Children =
      {
        // Prepare & depart lives only under Manifest (single visual parent).
        _btnTravel, _travelSystem,
        _btnStep, _btnContinue, _btnResume, _btnPause, _btnSave,
      },
    };
    var clockRow = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 8,
      Children =
      {
        new TextBlock
        {
          Text = "Attention",
          VerticalAlignment = VerticalAlignment.Center,
          Foreground = CalypsoPalette.MutedBrush,
          FontSize = 11,
        },
        _attention,
        new TextBlock
        {
          Text = "Speed",
          VerticalAlignment = VerticalAlignment.Center,
          Foreground = CalypsoPalette.MutedBrush,
          FontSize = 11,
        },
        _speed,
        _speedLabel,
        _clockLine,
      },
    };
    _opsExpander = new Expander
    {
      Header = "Ops clock",
      IsExpanded = false,
      Margin = new Thickness(0, 4, 0, 0),
      Content = clockRow,
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
        _opsExpander,
      },
    };

    _map = new StarMapControl
    {
      MinHeight = 260,
      FieldBrush = CalypsoPalette.MapFieldBrush,
      ShowChartGrid = true,
    };
    AgentProperties.SetId(_map, "calypso.map");
    _map.StarSelected += OnStarSelected;
    _hubDetail = new TextBlock
    {
      Text = "Select a system → Travel here (when docked idle).",
      Foreground = CalypsoPalette.MutedBrush,
      TextWrapping = TextWrapping.Wrap,
      Margin = new Thickness(0, 8, 0, 0),
      FontSize = 12,
      FontFamily = CalypsoPalette.BodyFont,
    };
    AgentProperties.SetId(_hubDetail, "calypso.hubDetail");

    var mapTitle = new TextBlock
    {
      Text = "Near-Sol · select destination to travel",
      FontFamily = CalypsoPalette.DisplayFont,
      FontWeight = FontWeight.SemiBold,
      FontSize = 14,
      Foreground = CalypsoPalette.AccentBrush,
      Margin = new Thickness(0, 0, 0, 6),
    };
    var mapDock = new DockPanel { LastChildFill = true };
    DockPanel.SetDock(mapTitle, Dock.Top);
    DockPanel.SetDock(_hubDetail, Dock.Bottom);
    mapDock.Children.Add(mapTitle);
    mapDock.Children.Add(_hubDetail);
    mapDock.Children.Add(CalypsoTheme.MapAtmosphereHost(_map));

    var mapPanel = new Border
    {
      Background = CalypsoPalette.PanelBrush,
      BorderBrush = new SolidColorBrush(Color.Parse("#1e2c3c")),
      BorderThickness = new Thickness(1),
      Padding = new Thickness(10),
      CornerRadius = new CornerRadius(6),
      Child = mapDock,
    };

    _voyage = new TextBlock
    {
      Foreground = CalypsoPalette.AccentBrush,
      FontFamily = CalypsoPalette.DisplayFont,
      FontWeight = FontWeight.SemiBold,
      TextWrapping = TextWrapping.Wrap,
      FontSize = 18,
    };
    var cashChip = CalypsoTheme.MetricChip("Cash", "—", out _cashChipValue);
    var lifeChip = CalypsoTheme.MetricChip("Hull life", "—", out _lifeChipValue);
    var chipRow = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Children = { cashChip, lifeChip },
    };
    _hullStats = new TextBlock
    {
      Foreground = CalypsoPalette.BodyBrush,
      TextWrapping = TextWrapping.Wrap,
      FontSize = 12,
      FontFamily = CalypsoPalette.BodyFont,
    };
    _decision = new TextBlock
    {
      Foreground = CalypsoPalette.MutedBrush,
      TextWrapping = TextWrapping.Wrap,
      FontSize = 12,
      Margin = new Thickness(0, 4, 0, 0),
    };
    _coach = new TextBlock
    {
      Foreground = CalypsoPalette.AccentSoftBrush,
      TextWrapping = TextWrapping.Wrap,
      FontSize = 12,
      FontWeight = FontWeight.SemiBold,
      Margin = new Thickness(0, 2, 0, 0),
    };
    _softFail = new TextBlock
    {
      Foreground = CalypsoPalette.DangerBrush,
      TextWrapping = TextWrapping.Wrap,
      FontSize = 12,
    };
    _survival = new TextBlock
    {
      Foreground = CalypsoPalette.SuccessBrush,
      TextWrapping = TextWrapping.Wrap,
      FontSize = 12,
      FontWeight = FontWeight.SemiBold,
      Margin = new Thickness(0, 2, 0, 0),
    };
    AgentProperties.SetId(_voyage, "calypso.voyage");
    AgentProperties.SetId(_hullStats, "calypso.hull");
    AgentProperties.SetId(_decision, "calypso.decision");
    AgentProperties.SetId(_coach, "calypso.coach");
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
      FlashOk($"Profile → {p}");
    };

    _boardScope = new ComboBox
    {
      Width = 120,
      ItemsSource = new[] { "Mesh", "Dock" },
      // Avalonia play default: live dock — mesh digests are often empty early.
      SelectedIndex = 1,
    };
    AgentProperties.SetId(_boardScope, "calypso.boardScope", AgentRoleNames.ComboBox);
    _boardScope.SelectionChanged += (_, _) =>
    {
      if (_session is null || _boardScope.SelectedItem is not string name) return;
      _session.Player.DockBoardOnly = name.Equals("Dock", StringComparison.OrdinalIgnoreCase);
      RefreshDesk();
    };

    _btnAcceptSpot = CalypsoTheme.MakeButton("Accept freight at dock", "calypso.acceptSpot", CalypsoButtonKind.Primary);
    _btnAcceptCharter = CalypsoTheme.MakeButton("Accept goods charter", "calypso.acceptCharter", CalypsoButtonKind.Primary);
    _btnDepart = CalypsoTheme.MakeButton("Depart manifest", "calypso.depart", CalypsoButtonKind.Primary);
    _btnRefuseStandby = CalypsoTheme.MakeButton("Refuse standby", "calypso.refuseStandby", CalypsoButtonKind.Quiet);
    _btnAcceptStandby = CalypsoTheme.MakeButton("Accept standby", "calypso.acceptStandby", CalypsoButtonKind.Secondary);
    _btnWait = CalypsoTheme.MakeButton("Wait", "calypso.wait", CalypsoButtonKind.Quiet);
    _btnPremium = CalypsoTheme.MakeButton("Pay premium", "calypso.premium", CalypsoButtonKind.Secondary);
    _btnOverhaul = CalypsoTheme.MakeButton("Request overhaul", "calypso.overhaul", CalypsoButtonKind.Danger);
    _btnMarketBuy = CalypsoTheme.MakeButton("Buy ASK", "calypso.marketBuy", CalypsoButtonKind.Secondary);
    _btnMarketSell = CalypsoTheme.MakeButton("Sell into BID", "calypso.marketSell", CalypsoButtonKind.Secondary);
    _btnAcceptSpot.Click += (_, _) => AcceptSelectedSpot();
    _btnAcceptCharter.Click += (_, _) => AcceptSelectedCharter();
    _btnMarketBuy.Click += (_, _) => TradeSelectedMarket(buy: true);
    _btnMarketSell.Click += (_, _) => TradeSelectedMarket(buy: false);
    _btnDepart.Click += (_, _) =>
    {
      Enqueue(new PlayerOrder(PlayerOrderKind.DepartManifest));
      FlashOk("Depart queued");
    };
    _btnRefuseStandby.Click += (_, _) => Enqueue(new PlayerOrder(PlayerOrderKind.RefuseStandby));
    _btnAcceptStandby.Click += (_, _) => Enqueue(new PlayerOrder(PlayerOrderKind.AcceptStandby));
    _btnWait.Click += (_, _) => Enqueue(new PlayerOrder(PlayerOrderKind.Wait));
    _btnPremium.Click += (_, _) => Enqueue(new PlayerOrder(PlayerOrderKind.PayPremium));
    _btnOverhaul.Click += (_, _) => Enqueue(new PlayerOrder(PlayerOrderKind.RequestOverhaul));

    _spot = new ListBox
    {
      MinHeight = 120,
      MaxHeight = 220,
      ItemTemplate = CalypsoTheme.SpotContractTemplate(),
    };
    _charters = new ListBox
    {
      MinHeight = 80,
      MaxHeight = 140,
      ItemTemplate = CalypsoTheme.CharterContractTemplate(),
    };
    _market = new ListBox
    {
      MinHeight = 100,
      MaxHeight = 180,
      ItemTemplate = CalypsoTheme.StringRowTemplate(),
    };
    _manifest = new ListBox
    {
      MinHeight = 60,
      MaxHeight = 120,
      ItemTemplate = CalypsoTheme.StringRowTemplate(),
    };
    AgentProperties.SetId(_spot, "calypso.spot", AgentRoleNames.ListBox);
    AgentProperties.SetId(_charters, "calypso.charters", AgentRoleNames.ListBox);
    AgentProperties.SetId(_market, "calypso.market", AgentRoleNames.ListBox);
    AgentProperties.SetId(_manifest, "calypso.manifest", AgentRoleNames.ListBox);
    _spot.SelectionChanged += (_, _) =>
    {
      HighlightSelectedSpotRoute();
      UpdateAcceptButtons();
    };
    _charters.SelectionChanged += (_, _) =>
    {
      HighlightSelectedCharterRoute();
      UpdateAcceptButtons();
    };

    _intelTabs = new TabControl();
    AgentProperties.SetId(_intelTabs, "calypso.boards", AgentRoleNames.TabControl);
    _intelTabs.Items.Add(MakeTab("Spot freight", new StackPanel
    {
      Spacing = 8,
      Children =
      {
        new TextBlock
        {
          Text = "Raw / Capital — accept only AT DOCK. Remotes are INTEL (Travel first).",
          Foreground = CalypsoPalette.MutedBrush,
          FontSize = 11,
          TextWrapping = TextWrapping.Wrap,
        },
        new StackPanel
        {
          Orientation = Orientation.Horizontal,
          Spacing = 8,
          Children =
          {
            new TextBlock { Text = "Filter", VerticalAlignment = VerticalAlignment.Center, Foreground = CalypsoPalette.MutedBrush, FontSize = 11 },
            _boardScope,
          },
        },
        _spot,
        _btnAcceptSpot,
      },
    }));
    _intelTabs.Items.Add(MakeTab("Goods charters", new StackPanel
    {
      Spacing = 8,
      Children =
      {
        new TextBlock
        {
          Text = "Firm escrows Final cargo and pays a sum A→B. Take only at this dock.",
          Foreground = CalypsoPalette.MutedBrush,
          FontSize = 11,
          TextWrapping = TextWrapping.Wrap,
        },
        _charters,
        new WrapPanel { Children = { _btnAcceptCharter, _btnAcceptStandby, _btnRefuseStandby } },
      },
    }));
    _intelTabs.Items.Add(MakeTab("Market", new StackPanel
    {
      Spacing = 8,
      Children =
      {
        new TextBlock
        {
          Text = "Dock HubOrders — Buy ASKs into hold stock; Sell stock into BIDs.",
          Foreground = CalypsoPalette.MutedBrush,
          FontSize = 11,
          TextWrapping = TextWrapping.Wrap,
        },
        _market,
        new WrapPanel { Children = { _btnMarketBuy, _btnMarketSell } },
      },
    }));
    _intelTabs.Items.Add(MakeTab("Manifest", new StackPanel
    {
      Spacing = 8,
      Children =
      {
        _manifest,
        new WrapPanel { Children = { _btnDepart, _btnPrepareDepart } },
        new TextBlock
        {
          Text = "Action stack",
          Foreground = CalypsoPalette.MutedBrush,
          FontSize = 11,
        },
        _intentStack,
        _btnCancelStack,
      },
    }));

    var voyagePanel = CalypsoTheme.Section("Voyage", new StackPanel
    {
      Spacing = 6,
      Children =
      {
        _voyage,
        chipRow,
        _hullStats,
        _decision,
        _coach,
        _survival,
        _softFail,
        new StackPanel
        {
          Orientation = Orientation.Horizontal,
          Spacing = 8,
          Children =
          {
            new TextBlock { Text = "Profile", VerticalAlignment = VerticalAlignment.Center, Foreground = CalypsoPalette.MutedBrush },
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
      FontFamily = CalypsoPalette.MonoFont,
      FontSize = 12,
    };

    var ledgerTabs = new TabControl();
    ledgerTabs.Items.Add(MakeTab("Registry", _registry));
    ledgerTabs.Items.Add(MakeTab("Money", WrapScroll(_money)));
    ledgerTabs.Items.Add(MakeTab("Agents", WrapScroll(_agents)));
    ledgerTabs.Items.Add(MakeTab("Raw", _raw));
    _papersExpander = new Expander
    {
      Header = "Ship papers",
      IsExpanded = false,
      Content = new StackPanel
      {
        Spacing = 8,
        Children =
        {
          CalypsoTheme.Section("Ledgers", _ledgers),
          ledgerTabs,
        },
      },
    };

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
          CalypsoTheme.Section("Boards", _intelTabs),
          CalypsoTheme.Section("Mesh", _feed),
          CalypsoTheme.Section("Life moments", _scorecard),
          _papersExpander,
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
              localBoard: _options.Board == JobBoardScope.Dock,
              lastTramp: _options.LastTramp);
          }

          _session = session;
          _deskService = new CaptainDeskService(session);
          _sessionSurface = SessionSurface.AttachAll(
            _deskService,
            preferredPipeName: SessionEndpoints.SinsPipeName);
          if (_sessionSurface?.HttpBaseUrl is { } httpUrl)
            _feedback.SetStatus($"Session HTTP {httpUrl}");
          session.PauseMode = CaptainPauseMode.UntilDecision;
          if (_options.Player)
          {
            // Interactive desk: hard-pause on berth decisions so idle days don't auto-burn cash.
            session.Player.Attention = DecisionAttention.HardPause;
            session.Player.SimSpeedScale = 1.0;
            // Live dock board — mesh digests are often empty for early play.
            session.Player.DockBoardOnly = true;
          }
          else
          {
            session.PauseMode = CaptainPauseMode.Never;
          }
          session.DayEnded += () => Dispatcher.UIThread.Post(RefreshDesk);
          session.AwaitingDecision += () => Dispatcher.UIThread.Post(() =>
          {
            RefreshDesk();
            _feedback.ClearBusy();
            NotifyTravelOutcome();
            var coach = _desk?.CoachLine;
            FlashOk(string.IsNullOrEmpty(coach)
              ? "Decision — dock act or travel"
              : coach);
            _feedback.SetStatus("Attention · Accept only at load dock · Travel via map");
          });

          Dispatcher.UIThread.Post(() =>
          {
            RefreshDesk();
            _feedback.ClearBusy();
            _feedback.SetStatus(_options.Player
              ? "Run always · set Attention/Speed · stack dock acts · Save checkpoints"
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
            FlashOk($"Horizon complete — {briefing.LifeMomentHits} life moments");
            SetTransportEnabled(false);
          });
        }
        catch (Exception ex)
        {
          CrashGuard.Report(ex, "MainWindow.RunAsync", openEditor: true, writeMiniDump: false);
          Dispatcher.UIThread.Post(() =>
          {
            _feedback.ClearBusy();
            FlashErr(ex.Message);
          });
        }
      });
    }
    catch (Exception ex)
    {
      CrashGuard.Report(ex, "MainWindow.StartCampaign", openEditor: true, writeMiniDump: false);
      _feedback.ClearBusy();
      FlashErr(ex.Message);
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
      FlashOk($"Saved {record.Label} ({record.Id:N})");
      _feedback.SetStatus($"Checkpoint → {CampaignSaveStore.Default.RootPath}");
    }
    catch (Exception ex)
    {
      CrashGuard.Report(ex, "MainWindow.Save", openEditor: false, writeMiniDump: false);
      FlashErr(ex.Message);
    }
  }

  void RefreshDesk()
  {
    if (_session is null) return;
    var desk = _session.LastDesk ?? _session.CaptureDesk();
    _desk = desk;
    _subtitle.Text = desk.SubtitleLine;
    _voyage.Text = desk.VoyageLine;
    _cashChipValue.Text = desk.CashLine;
    var lifeMatch = System.Text.RegularExpressions.Regex.Match(desk.HullLine, @"life (\d+)%");
    _lifeChipValue.Text = lifeMatch.Success ? lifeMatch.Groups[1].Value + "%" : "—";
    _hullStats.Text = $"{desk.StandingLine}\n{desk.HullLine}\n{desk.HoldLine}";
    _decision.Text = desk.DecisionLine;
    _coach.Text = desk.CoachLine;
    _coach.IsVisible = !string.IsNullOrEmpty(desk.CoachLine);
    _survival.Text = desk.SurvivalLine;
    _survival.IsVisible = !string.IsNullOrEmpty(desk.SurvivalLine);
    _survival.Foreground = desk.SurvivalLine.Contains("WIN", StringComparison.Ordinal)
      ? CalypsoPalette.SuccessBrush
      : desk.SurvivalLine.Contains("LOSE", StringComparison.Ordinal)
        ? CalypsoPalette.DangerBrush
        : CalypsoPalette.MutedBrush;
    _softFail.Text = desk.SoftFailLine;
    _softFail.IsVisible = !string.IsNullOrEmpty(desk.SoftFailLine);
    _map.SetMap(desk.MapPoints, desk.MapEdges);
    _map.SetShipMarker(desk.ShipMapX, desk.ShipMapY, desk.ShipMapVisible);
    ApplyRouteHighlight();

    _spot.ItemsSource = desk.SpotJobs
      .Select((j, i) => new SpotContractRow(
        j.Label,
        j.AtOrigin
          ? $"Pay {j.ContractPay:0} · Lift {j.LiftCost:0} · Net Δ{j.Margin:0.#} · ×{j.Quantity:0} · [{j.DistanceHint}]"
          : $"INTEL · Pay {j.ContractPay:0} · Net Δ{j.Margin:0.#} · Travel → {j.OriginName} first",
        j.AtOrigin,
        i))
      .ToList();
    _charters.ItemsSource = desk.Charters
      .Select((c, i) => new CharterContractRow(
        c.Label,
        c.Kind.Equals("standby", StringComparison.OrdinalIgnoreCase)
          ? c.Detail
          : $"Pay {c.ContractPay:0} · Lift {c.LiftCost:0} · Net Δ{c.Margin:0.#} · {c.Detail}",
        c.CanAcceptHere,
        i))
      .ToList();
    _market.ItemsSource = desk.MarketLots
      .Select(m => m.Summary)
      .ToList();
    _manifest.ItemsSource = desk.ManifestLines.Count > 0
      ? desk.ManifestLines
      : new List<string> { "(empty — accept freight/charter at this dock)" };
    _intentStack.ItemsSource = desk.IntentStackLines.Count > 0
      ? desk.IntentStackLines.ToList()
      : new List<string> { "(stack empty)" };
    _clockLine.Text =
      $"clock {desk.AttentionLine} · speed {desk.SimSpeedScale:0.##} · {desk.PaceLine}";
    SyncClockUi(desk);

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
    // Keep Travel armed at a remote berth; never leave the box on the current system.
    var travelBox = _travelSystem.Text?.Trim();
    var arm = desk.TravelTargetSystemId ?? desk.SuggestedTravelSystemId;
    if (!string.IsNullOrEmpty(arm)
        && (string.IsNullOrEmpty(travelBox)
            || travelBox.Equals(desk.CurrentSystemId, StringComparison.OrdinalIgnoreCase)))
    {
      _travelSystem.Text = arm;
    }

    UpdateTravelEnabled();
    UpdateAcceptButtons();
    _btnMarketBuy.IsEnabled = desk.DockedIdle;
    _btnMarketSell.IsEnabled = desk.DockedIdle;
    _btnDepart.IsEnabled = desk.DockedIdle && desk.ManifestUsed > 0m;

    if (desk.SoftFail) FlashErr(desk.SoftFailLine);
    if (_session.IsWaitingForCaptain && !desk.Complete)
    {
      _feedback.ClearBusy();
      _feedback.SetStatus($"Day {desk.Day} — {desk.VoyageLine}");
    }
  }

  void AcceptSelectedSpot()
  {
    if (_desk is null || _spot.SelectedIndex < 0 || _spot.SelectedIndex >= _desk.SpotJobs.Count)
    {
      FlashOk("Select a freight posting");
      return;
    }

    var job = _desk.SpotJobs[_spot.SelectedIndex];
    if (!job.AtOrigin)
    {
      _travelSystem.Text = job.OriginSystemId;
      if (_session is not null)
      {
        _session.Player.TravelTargetSystemId = job.OriginSystemId;
      }

      FlashOk($"Not at load dock — Travel → {job.OriginName}");
      TravelToSelection();
      return;
    }

    DeskExec(new SessionCommandDto
    {
      ActionId = SessionActionIds.AcceptSpot,
      Index = _spot.SelectedIndex,
    });
  }

  void UpdateAcceptButtons()
  {
    var docked = _desk is { DockedIdle: true };
    var spotOk = false;
    if (docked && _desk is not null
        && _spot.SelectedIndex >= 0
        && _spot.SelectedIndex < _desk.SpotJobs.Count)
    {
      spotOk = _desk.SpotJobs[_spot.SelectedIndex].AtOrigin;
    }

    var charterOk = false;
    if (docked && _desk is not null
        && _charters.SelectedIndex >= 0
        && _charters.SelectedIndex < _desk.Charters.Count)
    {
      charterOk = _desk.Charters[_charters.SelectedIndex].CanAcceptHere;
    }

    _btnAcceptSpot.IsEnabled = spotOk;
    _btnAcceptCharter.IsEnabled = charterOk;
  }

  void UpdateTravelEnabled()
  {
    var dest = ResolveTravelDest();
    var here = _desk?.CurrentSystemId;
    var can = _desk is { DockedIdle: true }
              && !string.IsNullOrEmpty(dest)
              && !dest.Equals(here, StringComparison.OrdinalIgnoreCase);
    _btnTravel.IsEnabled = can;
    ApplyRouteHighlight();
  }

  string? ResolveTravelDest()
  {
    var typed = _travelSystem.Text?.Trim();
    var here = _desk?.CurrentSystemId;
    if (!string.IsNullOrEmpty(typed)
        && !typed.Equals(here, StringComparison.OrdinalIgnoreCase))
    {
      return typed;
    }

    if (!string.IsNullOrEmpty(_mapSelection)
        && !_mapSelection.Equals(here, StringComparison.OrdinalIgnoreCase))
    {
      return _mapSelection;
    }

    return _desk?.TravelTargetSystemId ?? _desk?.SuggestedTravelSystemId;
  }

  void TravelToSelection()
  {
    var dest = ResolveTravelDest();
    if (_session is null || _deskService is null || string.IsNullOrEmpty(dest))
    {
      FlashOk("Select a system on the map or type a system id");
      return;
    }

    if (dest.Equals(_desk?.CurrentSystemId, StringComparison.OrdinalIgnoreCase))
    {
      dest = _desk?.SuggestedTravelSystemId;
      if (string.IsNullOrEmpty(dest))
      {
        FlashOk("Already here — pick another system (or wait for mesh freight)");
        return;
      }

      _travelSystem.Text = dest;
      FlashOk($"No berth freight — steaming to {dest}");
    }

    _pendingTravelDest = dest;
    _pendingTravelOrigin = _desk?.CurrentSystemId;
    _routeOrigin = _desk?.CurrentSystemId;
    _routeDest = dest;
    _routePathWarned = false;
    ApplyRouteHighlight();

    var result = _deskService.Execute(new SessionCommandDto
    {
      ActionId = SessionActionIds.Travel,
      DestSystemId = dest,
    });

    // Busy only when hull can't act (underway / grounded), not when pulse is slow.
    if (!result.Ok)
    {
      FlashErr(result.Message);
      if (result.ErrorCode is PlayerActionErrorCodes.AlreadyHere or PlayerActionErrorCodes.NoRoute
          or PlayerActionErrorCodes.UnknownDest or PlayerActionErrorCodes.Incomplete)
      {
        _routeOrigin = null;
        _routeDest = null;
        _pendingTravelDest = null;
        ApplyRouteHighlight();
      }

      RefreshDesk();
      return;
    }

    FlashOk(result.Message);
    RefreshDesk();
  }

  void NotifyTravelOutcome()
  {
    if (_session is null || string.IsNullOrEmpty(_pendingTravelDest) || _desk is null)
    {
      return;
    }

    var last = _session.Player.LastAction;
    var stillAtOrigin = _desk.DockedIdle
                        && !string.IsNullOrEmpty(_pendingTravelOrigin)
                        && _desk.CurrentSystemId.Equals(_pendingTravelOrigin, StringComparison.OrdinalIgnoreCase)
                        && !_desk.Underway;

    if (last is { ActionId: "travel", Ok: false })
    {
      FlashErr(last.Message);
      if (last.ErrorCode is PlayerActionErrorCodes.AlreadyHere or PlayerActionErrorCodes.NoRoute
          or PlayerActionErrorCodes.UnknownDest)
      {
        _routeOrigin = null;
        _routeDest = null;
        ApplyRouteHighlight();
      }

      if (last.ErrorCode != PlayerActionErrorCodes.Bunkering)
      {
        _pendingTravelDest = null;
        _pendingTravelOrigin = null;
      }

      return;
    }

    if (stillAtOrigin && last is null or { ActionId: "travel", Ok: true })
    {
      // Order may not have settled yet, or silent stall — surface decision text.
      var line = _desk.DecisionLine;
      if (line.Contains("failed", StringComparison.OrdinalIgnoreCase)
          || line.Contains("no route", StringComparison.OrdinalIgnoreCase)
          || line.Contains("unknown", StringComparison.OrdinalIgnoreCase)
          || line.Contains("already", StringComparison.OrdinalIgnoreCase)
          || line.Contains("busy", StringComparison.OrdinalIgnoreCase)
          || line.Contains("registry", StringComparison.OrdinalIgnoreCase))
      {
        FlashErr(line);
        _pendingTravelDest = null;
        _pendingTravelOrigin = null;
        return;
      }
    }

    if (_desk.Underway || _desk.VoyageLine.Contains("REPOSITION", StringComparison.Ordinal)
        || _desk.DecisionLine.Contains("awaiting departure", StringComparison.OrdinalIgnoreCase)
        || _desk.DecisionLine.Contains("bunkering", StringComparison.OrdinalIgnoreCase))
    {
      _pendingTravelDest = null;
      _pendingTravelOrigin = null;
    }
  }

  void AcceptSelectedCharter()
  {
    if (_desk is null || _charters.SelectedIndex < 0
        || _charters.SelectedIndex >= _desk.Charters.Count)
    {
      FlashOk("Select a goods charter");
      return;
    }

    DeskExec(new SessionCommandDto
    {
      ActionId = SessionActionIds.AcceptCharter,
      Index = _charters.SelectedIndex,
    });
  }

  void TradeSelectedMarket(bool buy)
  {
    if (_desk is null || _market.SelectedIndex < 0
        || _market.SelectedIndex >= _desk.MarketLots.Count)
    {
      FlashOk("Select a market lot");
      return;
    }

    DeskExec(new SessionCommandDto
    {
      ActionId = buy ? SessionActionIds.MarketBuy : SessionActionIds.MarketSell,
      Index = _market.SelectedIndex,
    });
  }

  void HighlightSelectedSpotRoute()
  {
    if (_desk is null || _spot.SelectedIndex < 0 || _spot.SelectedIndex >= _desk.SpotJobs.Count)
    {
      return;
    }

    var job = _desk.SpotJobs[_spot.SelectedIndex];
    _routeOrigin = job.OriginSystemId;
    _routeDest = job.DestSystemId;
    _map.SelectedId = job.OriginSystemId;
    if (!job.AtOrigin)
    {
      _mapSelection = job.OriginSystemId;
      _travelSystem.Text = job.OriginSystemId;
      if (_session is not null)
      {
        _session.Player.TravelTargetSystemId = job.OriginSystemId;
      }

      UpdateTravelEnabled();
    }

    ApplyRouteHighlight();
  }

  void HighlightSelectedCharterRoute()
  {
    if (_desk is null || _charters.SelectedIndex < 0 || _charters.SelectedIndex >= _desk.Charters.Count)
    {
      return;
    }

    var c = _desk.Charters[_charters.SelectedIndex];
    if (string.IsNullOrEmpty(c.OriginSystemId) || string.IsNullOrEmpty(c.DestSystemId))
    {
      return;
    }

    _routeOrigin = c.OriginSystemId;
    _routeDest = c.DestSystemId;
    _map.SelectedId = c.OriginSystemId;
    ApplyRouteHighlight();
  }

  void ApplyRouteHighlight()
  {
    if (_session is null || _desk is null)
    {
      _map.SetRoute(null);
      return;
    }

    var parts = new List<IReadOnlyList<StarMapEdge>>();
    if (_desk.UnderwayRoute.Count > 0)
    {
      parts.Add(_desk.UnderwayRoute);
    }

    var travel = ResolveTravelDest();
    if (!string.IsNullOrEmpty(travel)
        && !travel.Equals(_desk.CurrentSystemId, StringComparison.OrdinalIgnoreCase))
    {
      var planned = RouteHighlight.BetweenSystems(_session.Ids, _desk.CurrentSystemId, travel);
      if (planned.Count == 0)
      {
        if (!_routePathWarned)
        {
          _routePathWarned = true;
          FlashOk($"No graph path → {travel}");
        }
      }
      else
      {
        _routePathWarned = false;
        parts.Add(planned);
      }
    }

    if (!string.IsNullOrEmpty(_routeOrigin) && !string.IsNullOrEmpty(_routeDest)
        && !_routeDest.Equals(_desk.CurrentSystemId, StringComparison.OrdinalIgnoreCase))
    {
      var committed = RouteHighlight.BetweenSystems(_session.Ids, _routeOrigin, _routeDest);
      if (committed.Count == 0)
      {
        _routeOrigin = null;
        _routeDest = null;
        if (!_routePathWarned)
        {
          _routePathWarned = true;
          FlashOk("No graph path for travel highlight");
        }
      }
      else
      {
        parts.Add(committed);
      }
    }

    _map.SetRoute(parts.Count == 0 ? null : RouteHighlight.Merge(parts.ToArray()));
  }

  void Enqueue(PlayerOrder order)
  {
    if (_deskService is null) return;
    var actionId = order.Kind switch
    {
      PlayerOrderKind.Wait => SessionActionIds.Wait,
      PlayerOrderKind.PayPremium => SessionActionIds.Premium,
      PlayerOrderKind.RequestOverhaul => SessionActionIds.Overhaul,
      PlayerOrderKind.RefuseStandby => SessionActionIds.RefuseStandby,
      PlayerOrderKind.AcceptStandby => SessionActionIds.AcceptStandby,
      PlayerOrderKind.DepartManifest => SessionActionIds.Depart,
      _ => order.Kind.ToString(),
    };
    DeskExec(new SessionCommandDto { ActionId = actionId, Sku = order.SkuLabel });
  }

  void DeskExec(SessionCommandDto command)
  {
    if (_deskService is null) return;
    var result = _deskService.Execute(command);
    if (result.Ok)
      FlashOk(result.Message);
    else
      FlashErr(result.Message);
    RefreshDesk();
  }

  void ApplyClockFromUi()
  {
    if (_syncingClock || _session is null || _deskService is null) return;
    var attention = _attention.SelectedIndex switch
    {
      1 => "softSlow",
      2 => "hardPause",
      _ => "runAlways",
    };
    DeskExec(new SessionCommandDto
    {
      ActionId = SessionActionIds.SetClock,
      Attention = attention,
      Speed = _speed.Value,
    });
  }

  void SyncClockUi(CaptainDeskModel desk)
  {
    _syncingClock = true;
    try
    {
      var idx = desk.AttentionLine switch
      {
        "softSlow" => 1,
        "hardPause" => 2,
        _ => 0,
      };
      if (_attention.SelectedIndex != idx)
      {
        _attention.SelectedIndex = idx;
      }

      if (Math.Abs(_speed.Value - desk.SimSpeedScale) > 0.001)
      {
        _speed.Value = desk.SimSpeedScale;
      }

      _speedLabel.Text = SpeedLabel(desk.SimSpeedScale);
    }
    finally
    {
      _syncingClock = false;
    }
  }

  static string SpeedLabel(double scale) => scale switch
  {
    >= 0.99 => "Speed Max",
    >= 0.74 => "Speed Fast",
    >= 0.4 => "Speed Play",
    _ => "Speed Crawl",
  };

  void SetTransportEnabled(bool on)
  {
    _btnStep.IsEnabled = on;
    _btnContinue.IsEnabled = on;
    _btnResume.IsEnabled = on;
    _btnPause.IsEnabled = on;
    _btnTravel.IsEnabled = on;
    _btnAcceptSpot.IsEnabled = on;
    _btnAcceptCharter.IsEnabled = on;
    _btnMarketBuy.IsEnabled = on;
    _btnMarketSell.IsEnabled = on;
    _btnDepart.IsEnabled = on;
    _btnPrepareDepart.IsEnabled = on;
    _btnCancelStack.IsEnabled = on;
    _attention.IsEnabled = on;
    _speed.IsEnabled = on;
  }

  void OnStarSelected(string id)
  {
    _mapSelection = id;
    var here = _desk?.CurrentSystemId;
    var atHere = !string.IsNullOrEmpty(here)
                 && id.Equals(here, StringComparison.OrdinalIgnoreCase);
    if (atHere)
    {
      // Don't arm Travel on self — that soft-locks barren docks with "already at dock".
      var escape = _desk?.SuggestedTravelSystemId ?? _desk?.TravelTargetSystemId;
      if (!string.IsNullOrEmpty(escape)
          && !escape.Equals(here, StringComparison.OrdinalIgnoreCase))
      {
        if (_session is not null)
        {
          _session.Player.TravelTargetSystemId = escape;
        }

        _travelSystem.Text = escape;
      }
    }
    else
    {
      if (_session is not null)
      {
        _session.Player.TravelTargetSystemId = id;
      }

      _travelSystem.Text = id;
    }

    if (_desk?.HubDetails.TryGetValue(id, out var hub) == true)
    {
      _hubDetail.Text = atHere
        ? $"{hub.Name} · HERE (docked)\n{hub.ProfileHint}"
          + (string.IsNullOrEmpty(_desk.SuggestedTravelSystemId)
            ? ""
            : $"\n→ NEXT empty steam: {_desk.SuggestedTravelSystemId}")
        : $"{hub.Name} · {hub.Role}\n{hub.ProfileHint}\n→ Travel here when idle";
      _hubDetail.Foreground = CalypsoPalette.BodyBrush;
    }
    else
    {
      _hubDetail.Text = id;
    }

    UpdateTravelEnabled();
  }

  static TabItem MakeTab(string header, Control content) =>
    new() { Header = header, Content = content };

  static Control WrapScroll(Control child) =>
    new ScrollViewer
    {
      HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
      Content = child,
    };

  void FlashOk(string message)
  {
    _flashClear?.Stop();
    _chrome.FlashLine.Text = message;
    _chrome.FlashLine.Foreground = CalypsoPalette.AccentBrush;
    _flashClear = new DispatcherTimer(TimeSpan.FromSeconds(3), DispatcherPriority.Normal, (_, _) =>
    {
      _chrome.FlashLine.Text = string.Empty;
      _flashClear?.Stop();
    });
    _flashClear.Start();
  }

  void FlashErr(string message)
  {
    _flashClear?.Stop();
    _chrome.FlashLine.Text = message;
    _chrome.FlashLine.Foreground = CalypsoPalette.DangerBrush;
    _flashClear = new DispatcherTimer(TimeSpan.FromSeconds(6), DispatcherPriority.Normal, (_, _) =>
    {
      _chrome.FlashLine.Text = string.Empty;
      _flashClear?.Stop();
    });
    _flashClear.Start();
  }
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
