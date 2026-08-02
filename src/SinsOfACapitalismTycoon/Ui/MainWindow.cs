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
using Novolis.Agent.Core;
using Novolis.Agent.Surface;
using SinsOfACapitalismTycoon.Cli;
using SinsOfACapitalismTycoon.Universe;

namespace SinsOfACapitalismTycoon.Ui;

/// <summary>Captain’s bridge: voyage, travel, spot/charter intel, dock manifest.</summary>
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
  readonly TextBlock _runwayChipValue;
  readonly TextBlock _decision;
  readonly TextBlock _coach;
  readonly Border _coachChrome;
  readonly TextBlock _softFail;
  readonly TextBlock _survival;
  readonly ListBox _spot;
  readonly ListBox _charters;
  readonly ListBox _market;
  readonly ListBox _manifest;
  readonly ComboBox _profile;
  readonly ComboBox _boardScope;
  StackPanel? _boardFilterRow;
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
  CaptainBridgeService? _bridgeService;
  AgentSurface? _sessionSurface;
  CaptainBridgeModel? _bridge;
  CampaignBriefingModel? _briefing;
  decimal _priorCash = -1m;
  bool _priorMeshUnlocked;
  bool _priorSoftFail;
  int _priorGroundedDays;
  bool _softFailStickyFlashed;
  decimal _priorReputation = -1m;
  bool _lowRunwayWarned;
  string? _mapSelection;
  string? _routeOrigin;
  string? _routeDest;
  string? _pendingTravelDest;
  string? _pendingTravelOrigin;
  bool _syncingBoardScope;
  bool _routePathWarned;
  bool _syncingClock;

  public MainWindow(RunOptions options)
  {
    _options = options;
    CalypsoTheme.ApplyWindowChrome(this);
    Title = "Sins — Captain Bridge · ST Calypso";
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
      Text = "Captain Bridge",
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
    _btnTravel = CalypsoTheme.MakeButton("Travel here", "calypso.travel", CalypsoButtonKind.Secondary);
    _btnSave = CalypsoTheme.MakeButton("Save", "calypso.save", CalypsoButtonKind.Quiet);
    _btnCancelStack = CalypsoTheme.MakeButton("Cancel stack", "calypso.cancelStack", CalypsoButtonKind.Quiet);
    _btnPrepareDepart = CalypsoTheme.MakeButton("Prepare & depart", "calypso.prepareDepart", CalypsoButtonKind.Primary);
    _btnStep.Click += (_, _) => BridgeExec(new AgentCommand { ActionId = AgentActionIds.Step });
    _btnContinue.Click += (_, _) =>
    {
      BridgeExec(new AgentCommand { ActionId = AgentActionIds.Continue });
      _feedback.SetStatus("Running…");
    };
    _btnResume.Click += (_, _) =>
    {
      BridgeExec(new AgentCommand { ActionId = AgentActionIds.Resume });
      _feedback.SetStatus("Running to horizon…");
    };
    _btnPause.Click += (_, _) => { _session?.Pause(); _feedback.SetStatus("Will pause after current day"); };
    _btnTravel.Click += (_, _) => TravelToSelection();
    _btnSave.Click += (_, _) => _ = SaveCheckpointAsync();
    _btnCancelStack.Click += (_, _) =>
      BridgeExec(new AgentCommand { ActionId = AgentActionIds.CancelStack });
    _btnPrepareDepart.Click += (_, _) =>
      BridgeExec(new AgentCommand { ActionId = AgentActionIds.PrepareDepart }.With(AgentCommandKeys.Prepare, true));

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
    var runwayChip = CalypsoTheme.MetricChip("Runway", "—", out _runwayChipValue);
    var lifeChip = CalypsoTheme.MetricChip("Hull life", "—", out _lifeChipValue);
    var chipRow = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 8,
      Children = { cashChip, runwayChip, lifeChip },
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
      Foreground = CalypsoPalette.AccentBrush,
      TextWrapping = TextWrapping.Wrap,
      FontSize = 14,
      FontWeight = FontWeight.SemiBold,
      Margin = new Thickness(0),
    };
    _coachChrome = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#1a2838")),
      BorderBrush = CalypsoPalette.AccentBrush,
      BorderThickness = new Thickness(1, 1, 1, 1),
      CornerRadius = new CornerRadius(4),
      Padding = new Thickness(10, 8),
      Margin = new Thickness(0, 4, 0, 2),
      Child = _coach,
      IsVisible = false,
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
      if (_session is null || _syncingBoardScope) return;
      if (!_session.Player.MeshBoardUnlocked)
      {
        _session.Player.DockBoardOnly = true;
        return;
      }

      var dock = _boardScope.SelectedIndex == 1
                 || (_boardScope.SelectedItem is string name
                     && name.Equals("Dock", StringComparison.OrdinalIgnoreCase));
      _session.Player.DockBoardOnly = dock;
      FlashOk(dock ? "Board → Dock (live berth)" : "Board → Mesh (FTL digests)");
      RefreshCaptain();
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
          Text = "Pick a berth bet — Local accept, Steam on a rumor, or Wait.",
          Foreground = CalypsoPalette.MutedBrush,
          FontSize = 11,
          TextWrapping = TextWrapping.Wrap,
        },
        (_boardFilterRow = new StackPanel
        {
          Orientation = Orientation.Horizontal,
          Spacing = 8,
          IsVisible = false,
          Children =
          {
            new TextBlock { Text = "Filter", VerticalAlignment = VerticalAlignment.Center, Foreground = CalypsoPalette.MutedBrush, FontSize = 11 },
            _boardScope,
          },
        }),
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
        _coachChrome,
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
          ResetCaptainCeremonyState();
          NeuralAutopilotBootstrap.ApplyIfRequested(session, _options.NeuralAutopilot);
          _bridgeService = new CaptainBridgeService(session);
          _sessionSurface = AgentSurface.AttachAll(
            _bridgeService,
            CaptainAgentSurfaceContract.Definition,
            new AgentAttachOptions
            {
              EnableIpc = true,
              EnableHttp = true,
              EnableTcp = true,
              IpcAddress = SinsSessionEndpoints.PipeName,
            });
          if (_sessionSurface?.HttpBaseUrl is { } httpUrl)
            _feedback.SetStatus($"Session HTTP {httpUrl}");
          session.PauseMode = CaptainPauseMode.UntilDecision;
          if (_options.Player)
          {
            // Interactive UI: hard-pause on berth decisions so idle days don't auto-burn cash.
            session.Player.Attention = DecisionAttention.HardPause;
            session.Player.SimSpeedScale = 1.0;
            // Live dock board until first payday unlocks Mesh.
            session.Player.DockBoardOnly = true;
            MeshBoardUnlock.Sync(session.Player, session.Milestones);
          }
          else
          {
            session.PauseMode = CaptainPauseMode.Never;
          }
          session.DayEnded += () => Dispatcher.UIThread.Post(RefreshCaptain);
          session.AwaitingDecision += () => Dispatcher.UIThread.Post(() =>
          {
            RefreshCaptain();
            _feedback.ClearBusy();
            NotifyTravelOutcome();
            var coach = _bridge?.CoachLine;
            FlashOk(string.IsNullOrEmpty(coach)
              ? "Decision — dock act or travel"
              : coach);
            _feedback.SetStatus("Attention · pick a berth bet · Depart when staged");
          });

          Dispatcher.UIThread.Post(() =>
          {
            RefreshCaptain();
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
            FlashOk($"Horizon complete — {briefing.LifeMomentHits} life · {session.Fun.SummaryLine()}");
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

  void RefreshCaptain()
  {
    if (_session is null) return;
    // Prefer LastBridge (captured on the sim thread). Rebuild when board filter diverges
    // or no pulse snapshot exists yet. HubOrders snapshot is race-safe via SnapshotHubOrders.
    CaptainBridgeModel bridge;
    var last = _session.LastBridge;
    if (last is not null && CaptainMatchesBoardFilter(last))
    {
      bridge = last;
    }
    else
    {
      try
      {
        bridge = _session.CaptureBridge();
      }
      catch (NullReferenceException)
      {
        if (last is null)
        {
          throw;
        }

        bridge = last;
      }
    }

    _bridge = bridge;
    _subtitle.Text = bridge.SubtitleLine;
    _cashChipValue.Text = bridge.CashLine;
    _runwayChipValue.Text = bridge.RunwayDays >= 900m
      ? "—"
      : $"{bridge.RunwayDays:0.#}d";
    _runwayChipValue.Foreground = bridge.RunwayDays < 5m
      ? CalypsoPalette.DangerBrush
      : bridge.RunwayDays < 12m
        ? CalypsoPalette.AccentBrush
        : CalypsoPalette.SuccessBrush;
    var lifeMatch = System.Text.RegularExpressions.Regex.Match(bridge.HullLine, @"life (\d+)%");
    _lifeChipValue.Text = lifeMatch.Success ? lifeMatch.Groups[1].Value + "%" : "—";
    _hullStats.Text = $"{bridge.StandingLine}\n{bridge.HullLine}\n{bridge.HoldLine}";
    // Voyage carries clockwork escrow when underway (TTD "watch the train").
    _voyage.Text = string.IsNullOrEmpty(bridge.EscrowClockLine) || !bridge.Underway
      ? bridge.VoyageLine
      : $"{bridge.VoyageLine}\n{bridge.EscrowClockLine}";
    _decision.Text = bridge.DecisionLine;
    _coach.Text = bridge.CoachLine;
    _coachChrome.IsVisible = !string.IsNullOrEmpty(bridge.CoachLine);
    _survival.Text = bridge.SurvivalLine;
    _survival.IsVisible = !string.IsNullOrEmpty(bridge.SurvivalLine);
    _survival.Foreground = bridge.SurvivalLine.Contains("WIN", StringComparison.Ordinal)
      ? CalypsoPalette.SuccessBrush
      : bridge.SurvivalLine.Contains("LOSE", StringComparison.Ordinal)
        ? CalypsoPalette.DangerBrush
        : CalypsoPalette.MutedBrush;
    _softFail.Text = bridge.SoftFailLine;
    _softFail.IsVisible = !string.IsNullOrEmpty(bridge.SoftFailLine);
    _map.SetMap(bridge.MapPoints, bridge.MapEdges);
    _map.SetShipMarker(bridge.ShipMapX, bridge.ShipMapY, bridge.ShipMapVisible);
    ApplyRouteHighlight();

    _spot.ItemsSource = bridge.BerthOffers
      .Select((o, i) =>
      {
        var badge = o.Kind switch
        {
          BerthOfferKind.Local => o.Band.ToUpperInvariant(),
          BerthOfferKind.Rumor => "RUMOR",
          _ => "WAIT",
        };
        return new SpotContractRow(
          o.Title,
          string.IsNullOrEmpty(o.Hook) ? o.Detail : $"{o.Hook}\n{o.Detail}",
          AtDock: o.Kind == BerthOfferKind.Local,
          Index: i,
          Badge: badge,
          IsRumor: o.Kind == BerthOfferKind.Rumor,
          IsWait: o.Kind == BerthOfferKind.Wait,
          Band: o.Band);
      })
      .ToList();

    if (_boardFilterRow is not null)
    {
      _boardFilterRow.IsVisible = bridge.MeshBoardUnlocked;
    }

    if (bridge.MeshBoardUnlocked)
    {
      var wantDock = _session.Player.DockBoardOnly;
      var idx = wantDock ? 1 : 0;
      if (_boardScope.SelectedIndex != idx)
      {
        _syncingBoardScope = true;
        try
        {
          _boardScope.SelectedIndex = idx;
        }
        finally
        {
          _syncingBoardScope = false;
        }
      }
    }

    // Early game: voyage over ops — keep papers collapsed until payday.
    if (!bridge.MeshBoardUnlocked)
    {
      _opsExpander.IsExpanded = false;
      _papersExpander.IsExpanded = false;
      _opsExpander.IsVisible = false;
      _papersExpander.IsVisible = false;
    }
    else
    {
      _opsExpander.IsVisible = true;
      _papersExpander.IsVisible = true;
    }
    _charters.ItemsSource = bridge.Charters
      .Select((c, i) => new CharterContractRow(
        c.Label,
        c.Kind.Equals("standby", StringComparison.OrdinalIgnoreCase)
          ? c.Detail
          : $"Pay {c.ContractPay:0} · Lift {c.LiftCost:0} · Net Δ{c.Margin:0.#} · {c.Detail}",
        c.CanAcceptHere,
        i))
      .ToList();
    _market.ItemsSource = bridge.MarketLots
      .Select(m => m.Summary)
      .ToList();
    _manifest.ItemsSource = bridge.ManifestLines.Count > 0
      ? bridge.ManifestLines
      : new List<string> { "(empty — accept freight/charter at this dock)" };
    _intentStack.ItemsSource = bridge.IntentStackLines.Count > 0
      ? bridge.IntentStackLines.ToList()
      : new List<string> { "(stack empty)" };
    _clockLine.Text =
      $"clock {bridge.AttentionLine} · speed {bridge.SimSpeedScale:0.##} · {bridge.PaceLine}";
    SyncClockUi(bridge);

    _feed.SetLines(bridge.Feed);
    _scorecard.SetRows(bridge.Scorecard, bridge.ScorecardTitle);
    _ledgers.SetPair("Calypso", bridge.CashLine, "Ops",
      bridge.MoneyRows.FirstOrDefault(r => r.Key == "Ops liquid")?.Value ?? "—",
      "Never summed with Core");
    _registry.SetRows(bridge.RegistryRows);
    _money.SetRows(bridge.MoneyRows);
    _agents.SetRows(bridge.AgentRows);

    _btnRefuseStandby.IsEnabled = bridge.StandbyOffer;
    _btnAcceptStandby.IsEnabled = bridge.StandbyOffer;
    // Keep Travel armed at a remote berth; never leave the box on the current system.
    var travelBox = _travelSystem.Text?.Trim();
    var arm = bridge.TravelTargetSystemId ?? bridge.SuggestedTravelSystemId;
    if (!string.IsNullOrEmpty(arm)
        && (string.IsNullOrEmpty(travelBox)
            || travelBox.Equals(bridge.CurrentSystemId, StringComparison.OrdinalIgnoreCase)))
    {
      _travelSystem.Text = arm;
    }

    UpdateTravelEnabled();
    UpdateAcceptButtons();
    _btnMarketBuy.IsEnabled = bridge.DockedIdle;
    _btnMarketSell.IsEnabled = bridge.DockedIdle;
    _btnDepart.IsEnabled = bridge.DockedIdle && bridge.ManifestUsed > 0m;

    FireCaptainCeremonies(bridge);

    if (_session.IsWaitingForCaptain && !bridge.Complete)
    {
      _feedback.ClearBusy();
      _feedback.SetStatus($"Day {bridge.Day} — {bridge.VoyageLine}");
    }
  }

  bool CaptainMatchesBoardFilter(CaptainBridgeModel bridge)
  {
    if (_session is null)
    {
      return true;
    }

    var wantDock = _session.Player.DockBoardOnly;
    var showsDock = bridge.SubtitleLine.Contains("intel dock", StringComparison.Ordinal);
    return wantDock == showsDock;
  }

  void FireCaptainCeremonies(CaptainBridgeModel bridge)
  {
    if (_session is null)
    {
      return;
    }

    var cash = ParseCaptainCash(bridge.CashLine);
    var grounded = _session.Player.SoftFailGroundedDays;

    // First paint: seed priors so load/save doesn't fake payday / unlock juice.
    if (_priorCash < 0m)
    {
      _priorCash = cash;
      _priorMeshUnlocked = bridge.MeshBoardUnlocked;
      _priorSoftFail = bridge.SoftFail;
      _priorGroundedDays = grounded;
      _softFailStickyFlashed = bridge.SoftFail;
      _priorReputation = bridge.ReputationScore;
      _lowRunwayWarned = bridge.RunwayDays < 5m;
      return;
    }

    // Cap+ bleed: low runway warning (once until recovered).
    if (bridge.RunwayDays < 5m && bridge.RunwayDays < 900m && !_lowRunwayWarned)
    {
      _lowRunwayWarned = true;
      FlashErr($"Runway thin — ~{bridge.RunwayDays:0.#}d @ {bridge.DailyPremium:0.#}/d · haul or settle");
    }
    else if (bridge.RunwayDays >= 8m)
    {
      _lowRunwayWarned = false;
    }

    // Payday / cash pulse (escrow release).
    if (cash > _priorCash + 40m)
    {
      var delta = cash - _priorCash;
      _session.Fun.NoteEscrowRelease();
      FlashOk($"CCA payday +{delta:0} · cash {cash:0} · {bridge.RunwayLine}");
    }

    // Mesh unlock ceremony (first escrow payday unlocks digests).
    if (!_priorMeshUnlocked && bridge.MeshBoardUnlocked)
    {
      _session.Fun.NoteMeshUnlock();
      FlashOk("Mesh digests unlocked — Filter: Mesh / Dock");
    }

    // TTD station-rating analogue: reputation lift (known-responsive / deliveries).
    if (_priorReputation >= 0m && bridge.ReputationScore >= _priorReputation + 6m)
    {
      _session.Fun.NoteReputationLift();
      FlashOk($"Reputation {bridge.ReputationScore:0} — board margins ease (known-responsive)");
    }

    // SoftFail near-miss (5–6d grounded).
    if (grounded is 5 or 6 && grounded != _priorGroundedDays && !bridge.SoftFail)
    {
      _session.Fun.NoteSoftFailNearMiss();
      FlashErr($"Near SoftFail — {7 - grounded}d left · settle premium / overhaul");
    }

    // SoftFail raised once.
    if (bridge.SoftFail && !_priorSoftFail)
    {
      _session.Fun.NoteSoftFailRaised();
      FlashErr(bridge.SoftFailLine);
      _softFailStickyFlashed = true;
    }
    else if (!bridge.SoftFail && _priorSoftFail)
    {
      _session.Fun.NoteSoftFailRecovery();
      FlashOk("Standing open again — operable");
      _softFailStickyFlashed = false;
    }
    else if (bridge.SoftFail && !_softFailStickyFlashed)
    {
      FlashErr(bridge.SoftFailLine);
      _softFailStickyFlashed = true;
    }

    _priorCash = cash;
    _priorMeshUnlocked = bridge.MeshBoardUnlocked;
    _priorSoftFail = bridge.SoftFail;
    _priorGroundedDays = grounded;
    _priorReputation = bridge.ReputationScore;
  }

  void ResetCaptainCeremonyState()
  {
    _priorCash = -1m;
    _priorMeshUnlocked = false;
    _priorSoftFail = false;
    _priorGroundedDays = 0;
    _softFailStickyFlashed = false;
    _priorReputation = -1m;
    _lowRunwayWarned = false;
  }

  static decimal ParseCaptainCash(string cashLine)
  {
    var normalized = cashLine.Replace(',', '.');
    return decimal.TryParse(
      normalized,
      System.Globalization.NumberStyles.Number,
      System.Globalization.CultureInfo.InvariantCulture,
      out var v)
      ? v
      : 0m;
  }

  void AcceptSelectedSpot()
  {
    if (_bridge is null || _spot.SelectedIndex < 0 || _spot.SelectedIndex >= _bridge.BerthOffers.Count)
    {
      FlashOk("Select a berth bet");
      return;
    }

    var offer = _bridge.BerthOffers[_spot.SelectedIndex];
    if (offer.Kind == BerthOfferKind.Wait)
    {
      Enqueue(new PlayerOrder(PlayerOrderKind.Wait));
      FlashOk("Wait — holding berth");
      return;
    }

    if (offer.Spot is null || offer.SpotIndex < 0 || offer.SpotIndex >= _bridge.SpotJobs.Count)
    {
      FlashOk("Offer has no freight");
      return;
    }

    var job = _bridge.SpotJobs[offer.SpotIndex];
    if (!job.AtOrigin || offer.Kind == BerthOfferKind.Rumor)
    {
      _travelSystem.Text = job.OriginSystemId;
      if (_session is not null)
      {
        _session.Player.TravelTargetSystemId = job.OriginSystemId;
      }

      FlashOk($"Steam for haul — → {job.OriginName}");
      TravelToSelection();
      return;
    }

    BridgeExec(new AgentCommand { ActionId = AgentActionIds.AcceptSpot }
      .With(AgentCommandKeys.Index, offer.SpotIndex));
  }

  void UpdateAcceptButtons()
  {
    var docked = _bridge is { DockedIdle: true };
    var meshBoardUnlocked = _bridge?.MeshBoardUnlocked == true;
    BerthOffer? offer = null;
    if (_bridge is not null
        && _spot.SelectedIndex >= 0
        && _spot.SelectedIndex < _bridge.BerthOffers.Count)
    {
      offer = _bridge.BerthOffers[_spot.SelectedIndex];
    }

    if (offer is { Kind: BerthOfferKind.Local })
    {
      _btnAcceptSpot.Content = "Accept at dock";
      _btnAcceptSpot.IsEnabled = docked;
      // SoASE focal CTA: berth Accept is primary; Travel demoted until Mesh unlock.
      _btnTravel.Opacity = meshBoardUnlocked ? 0.85 : 0.35;
      _btnTravel.IsVisible = true;
      _travelSystem.Opacity = meshBoardUnlocked ? 1 : 0.4;
    }
    else if (offer is { Kind: BerthOfferKind.Rumor })
    {
      _btnAcceptSpot.Content = "Steam for this haul";
      _btnAcceptSpot.IsEnabled = docked;
      _btnTravel.Opacity = 0.45;
      _travelSystem.Opacity = 0.7;
    }
    else if (offer is { Kind: BerthOfferKind.Wait })
    {
      _btnAcceptSpot.Content = "Wait";
      _btnAcceptSpot.IsEnabled = docked;
      _btnTravel.Opacity = meshBoardUnlocked ? 1 : 0.55;
      _btnTravel.IsVisible = true;
      _travelSystem.Opacity = meshBoardUnlocked ? 1 : 0.55;
    }
    else
    {
      _btnAcceptSpot.Content = "Accept freight at dock";
      _btnAcceptSpot.IsEnabled = false;
      _btnTravel.Opacity = 1;
      _btnTravel.IsVisible = true;
      _travelSystem.Opacity = 1;
    }

    var charterOk = false;
    if (docked && _bridge is not null
        && _charters.SelectedIndex >= 0
        && _charters.SelectedIndex < _bridge.Charters.Count)
    {
      charterOk = _bridge.Charters[_charters.SelectedIndex].CanAcceptHere;
    }

    _btnAcceptCharter.IsEnabled = charterOk;
  }

  void UpdateTravelEnabled()
  {
    var dest = ResolveTravelDest();
    var here = _bridge?.CurrentSystemId;
    var can = _bridge is { DockedIdle: true }
              && !string.IsNullOrEmpty(dest)
              && !dest.Equals(here, StringComparison.OrdinalIgnoreCase);
    _btnTravel.IsEnabled = can;
    ApplyRouteHighlight();
  }

  string? ResolveTravelDest()
  {
    var typed = _travelSystem.Text?.Trim();
    var here = _bridge?.CurrentSystemId;
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

    return _bridge?.TravelTargetSystemId ?? _bridge?.SuggestedTravelSystemId;
  }

  void TravelToSelection()
  {
    var dest = ResolveTravelDest();
    if (_session is null || _bridgeService is null || string.IsNullOrEmpty(dest))
    {
      FlashOk("Select a system on the map or type a system id");
      return;
    }

    if (dest.Equals(_bridge?.CurrentSystemId, StringComparison.OrdinalIgnoreCase))
    {
      dest = _bridge?.SuggestedTravelSystemId;
      if (string.IsNullOrEmpty(dest))
      {
        FlashOk("Already here — pick another system (or wait for mesh freight)");
        return;
      }

      _travelSystem.Text = dest;
      FlashOk($"No berth freight — steaming to {dest}");
    }

    _pendingTravelDest = dest;
    _pendingTravelOrigin = _bridge?.CurrentSystemId;
    _routeOrigin = _bridge?.CurrentSystemId;
    _routeDest = dest;
    _routePathWarned = false;
    ApplyRouteHighlight();

    var result = _bridgeService.Execute(new AgentCommand { ActionId = AgentActionIds.Travel }
      .With(AgentCommandKeys.DestSystemId, dest));

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

      RefreshCaptain();
      return;
    }

    FlashOk(result.Message);
    RefreshCaptain();
  }

  void NotifyTravelOutcome()
  {
    if (_session is null || string.IsNullOrEmpty(_pendingTravelDest) || _bridge is null)
    {
      return;
    }

    var last = _session.Player.LastAction;
    var stillAtOrigin = _bridge.DockedIdle
                        && !string.IsNullOrEmpty(_pendingTravelOrigin)
                        && _bridge.CurrentSystemId.Equals(_pendingTravelOrigin, StringComparison.OrdinalIgnoreCase)
                        && !_bridge.Underway;

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
      var line = _bridge.DecisionLine;
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

    if (_bridge.Underway || _bridge.VoyageLine.Contains("REPOSITION", StringComparison.Ordinal)
        || _bridge.DecisionLine.Contains("awaiting departure", StringComparison.OrdinalIgnoreCase)
        || _bridge.DecisionLine.Contains("bunkering", StringComparison.OrdinalIgnoreCase))
    {
      _pendingTravelDest = null;
      _pendingTravelOrigin = null;
    }
  }

  void AcceptSelectedCharter()
  {
    if (_bridge is null || _charters.SelectedIndex < 0
        || _charters.SelectedIndex >= _bridge.Charters.Count)
    {
      FlashOk("Select a goods charter");
      return;
    }

    BridgeExec(new AgentCommand { ActionId = AgentActionIds.AcceptCharter }
      .With(AgentCommandKeys.Index, _charters.SelectedIndex));
  }

  void TradeSelectedMarket(bool buy)
  {
    if (_bridge is null || _market.SelectedIndex < 0
        || _market.SelectedIndex >= _bridge.MarketLots.Count)
    {
      FlashOk("Select a market lot");
      return;
    }

    BridgeExec(new AgentCommand
      {
        ActionId = buy ? AgentActionIds.MarketBuy : AgentActionIds.MarketSell,
      }
      .With(AgentCommandKeys.Index, _market.SelectedIndex));
  }

  void HighlightSelectedSpotRoute()
  {
    if (_bridge is null || _spot.SelectedIndex < 0 || _spot.SelectedIndex >= _bridge.BerthOffers.Count)
    {
      return;
    }

    var offer = _bridge.BerthOffers[_spot.SelectedIndex];
    if (offer.Spot is null)
    {
      return;
    }

    var job = offer.Spot;
    _routeOrigin = job.OriginSystemId;
    _routeDest = job.DestSystemId;
    _map.SelectedId = job.OriginSystemId;
    if (!job.AtOrigin || offer.Kind == BerthOfferKind.Rumor)
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
    if (_bridge is null || _charters.SelectedIndex < 0 || _charters.SelectedIndex >= _bridge.Charters.Count)
    {
      return;
    }

    var c = _bridge.Charters[_charters.SelectedIndex];
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
    if (_session is null || _bridge is null)
    {
      _map.SetRoute(null);
      return;
    }

    var parts = new List<IReadOnlyList<StarMapEdge>>();
    if (_bridge.UnderwayRoute.Count > 0)
    {
      parts.Add(_bridge.UnderwayRoute);
    }

    var travel = ResolveTravelDest();
    if (!string.IsNullOrEmpty(travel)
        && !travel.Equals(_bridge.CurrentSystemId, StringComparison.OrdinalIgnoreCase))
    {
      var planned = RouteHighlight.BetweenSystems(_session.Ids, _bridge.CurrentSystemId, travel);
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
        && !_routeDest.Equals(_bridge.CurrentSystemId, StringComparison.OrdinalIgnoreCase))
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
    if (_bridgeService is null) return;
    var actionId = order.Kind switch
    {
      PlayerOrderKind.Wait => AgentActionIds.Wait,
      PlayerOrderKind.PayPremium => AgentActionIds.Premium,
      PlayerOrderKind.RequestOverhaul => AgentActionIds.Overhaul,
      PlayerOrderKind.RefuseStandby => AgentActionIds.RefuseStandby,
      PlayerOrderKind.AcceptStandby => AgentActionIds.AcceptStandby,
      PlayerOrderKind.DepartManifest => AgentActionIds.Depart,
      _ => order.Kind.ToString(),
    };
    BridgeExec(new AgentCommand { ActionId = actionId }.With(AgentCommandKeys.Sku, order.SkuLabel));
  }

  void BridgeExec(AgentCommand command)
  {
    if (_bridgeService is null) return;
    var result = _bridgeService.Execute(command);
    if (result.Ok)
      FlashOk(result.Message);
    else
      FlashErr(result.Message);
    RefreshCaptain();
  }

  void ApplyClockFromUi()
  {
    if (_syncingClock || _session is null || _bridgeService is null) return;
    var attention = _attention.SelectedIndex switch
    {
      1 => "softSlow",
      2 => "hardPause",
      _ => "runAlways",
    };
    BridgeExec(new AgentCommand { ActionId = AgentActionIds.SetClock }
      .With(AgentCommandKeys.Attention, attention)
      .With(AgentCommandKeys.Speed, _speed.Value));
  }

  void SyncClockUi(CaptainBridgeModel bridge)
  {
    _syncingClock = true;
    try
    {
      var idx = bridge.AttentionLine switch
      {
        "softSlow" => 1,
        "hardPause" => 2,
        _ => 0,
      };
      if (_attention.SelectedIndex != idx)
      {
        _attention.SelectedIndex = idx;
      }

      if (Math.Abs(_speed.Value - bridge.SimSpeedScale) > 0.001)
      {
        _speed.Value = bridge.SimSpeedScale;
      }

      _speedLabel.Text = SpeedLabel(bridge.SimSpeedScale);
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
    var here = _bridge?.CurrentSystemId;
    var atHere = !string.IsNullOrEmpty(here)
                 && id.Equals(here, StringComparison.OrdinalIgnoreCase);
    if (atHere)
    {
      // Don't arm Travel on self — that soft-locks barren docks with "already at dock".
      var escape = _bridge?.SuggestedTravelSystemId ?? _bridge?.TravelTargetSystemId;
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

    if (_bridge?.HubDetails.TryGetValue(id, out var hub) == true)
    {
      _hubDetail.Text = atHere
        ? $"{hub.Name} · HERE (docked)\n{hub.ProfileHint}"
          + (string.IsNullOrEmpty(_bridge.SuggestedTravelSystemId)
            ? ""
            : $"\n→ NEXT empty steam: {_bridge.SuggestedTravelSystemId}")
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
