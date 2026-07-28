using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Novolis.Avalonia.Briefing;
using Novolis.Avalonia.StarMap;
using Novolis.Avalonia.Studio;
using SinsOfACapitalismTycoon.Cli;
using SinsOfACapitalismTycoon.Universe;

namespace SinsOfACapitalismTycoon.Ui;

/// <summary>Campaign briefing room: progress while running, then map + radio + scorecard.</summary>
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
  readonly FeedPanel _feed;
  readonly ScorecardView _scorecard;
  readonly DualMetricStrip _ledgers;
  readonly MetricTableView _registry;
  readonly MetricTableView _logistics;
  readonly MetricTableView _money;
  readonly MetricTableView _agents;
  readonly MetricTableView _mega;
  readonly TextBlock _curtain;
  readonly TextBox _raw;
  readonly TabControl _tabs;
  readonly Grid _root;
  CampaignBriefingModel? _model;

  public MainWindow(RunOptions options)
  {
    _options = options;
    Title = "Sins of a Capitalism Tycoon";
    Width = 1280;
    Height = 820;
    MinWidth = 900;
    MinHeight = 560;
    Background = new SolidColorBrush(Color.Parse("#0b1020"));

    _chrome = StudioChrome.Create();
    _feedback = _chrome.CreateFeedback();

    var brand = new TextBlock
    {
      Text = "Sins",
      FontSize = 28,
      FontWeight = FontWeight.Bold,
      Foreground = BrandBrush,
    };
    var title = new TextBlock
    {
      Text = "of a Capitalism Tycoon",
      FontSize = 18,
      FontWeight = FontWeight.SemiBold,
      VerticalAlignment = VerticalAlignment.Bottom,
      Margin = new Thickness(10, 0, 0, 2),
      Foreground = new SolidColorBrush(Color.Parse("#e8e8e8")),
    };
    var subtitle = new TextBlock
    {
      Name = "Subtitle",
      Text = $"seed {_options.Seed} · {DurationArg.Format(_options.DaysHours)} · drama {(_options.Drama ? "on" : "off")}",
      Foreground = MutedBrush,
      FontSize = 12,
      Margin = new Thickness(0, 4, 0, 0),
    };

    var header = new StackPanel
    {
      Margin = new Thickness(16, 12, 16, 8),
      Children =
      {
        new StackPanel
        {
          Orientation = Orientation.Horizontal,
          Children = { brand, title },
        },
        subtitle,
        _chrome.StatusLine,
        _chrome.FlashLine,
      },
    };

    _map = new StarMapControl { MinHeight = 280 };
    _map.StarSelected += OnStarSelected;
    _hubDetail = new TextBlock
    {
      Text = "Select a hub on the map.",
      Foreground = MutedBrush,
      TextWrapping = TextWrapping.Wrap,
      Margin = new Thickness(0, 8, 0, 0),
      FontSize = 12,
    };

    var mapTitle = new TextBlock
    {
      Text = "Near-Sol campaign",
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

    _feed = new FeedPanel { MinHeight = 160 };
    _scorecard = new ScorecardView();
    _ledgers = new DualMetricStrip();
    _ledgers.SetPair("Ops", "…", "Core", "…", "Ops vs Core — never summed");
    _registry = new MetricTableView();
    _logistics = new MetricTableView();
    _money = new MetricTableView();
    _agents = new MetricTableView();
    _mega = new MetricTableView();
    _curtain = new TextBlock
    {
      Foreground = BrandBrush,
      FontStyle = FontStyle.Italic,
      TextWrapping = TextWrapping.Wrap,
      Margin = new Thickness(0, 8, 0, 0),
    };
    _raw = new TextBox
    {
      IsReadOnly = true,
      AcceptsReturn = true,
      TextWrapping = TextWrapping.NoWrap,
      FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New, monospace"),
      FontSize = 12,
    };

    _tabs = new TabControl();
    _tabs.Items.Add(MakeTab("Registry", _registry));
    _tabs.Items.Add(MakeTab("Money", WrapScroll(_money)));
    _tabs.Items.Add(MakeTab("Logistics", WrapScroll(_logistics)));
    _tabs.Items.Add(MakeTab("Agents", WrapScroll(_agents)));
    _tabs.Items.Add(MakeTab("Bulk River", WrapScroll(_mega)));
    _tabs.Items.Add(MakeTab("Raw", _raw));

    var rightScroll = new ScrollViewer
    {
      HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
      Content = new StackPanel
      {
        Spacing = 12,
        Margin = new Thickness(0, 0, 4, 0),
        Children =
        {
          Section("Radio", _feed),
          Section("Life moments", _scorecard),
          Section("Ledgers", _ledgers),
          _curtain,
          _tabs,
        },
      },
    };

    var body = new Grid
    {
      ColumnDefinitions = new ColumnDefinitions("1.2*,*"),
      ColumnSpacing = 12,
      Margin = new Thickness(16, 0, 16, 8),
      Children = { mapPanel, rightScroll },
    };
    Grid.SetColumn(rightScroll, 1);

    _root = new Grid
    {
      RowDefinitions = new RowDefinitions("Auto,*,Auto"),
      Children = { header, body, _chrome.BusyOverlay },
    };
    Grid.SetRow(body, 1);
    Grid.SetRow(_chrome.BusyOverlay, 0);
    Grid.SetRowSpan(_chrome.BusyOverlay, 3);

    Content = _root;
    Opened += async (_, _) => await RunCampaignAsync(subtitle);
  }

  async Task RunCampaignAsync(TextBlock subtitle)
  {
    _feedback.SetBusy("Campaign running…");
    _feedback.SetStatus("Seeding Near-Sol…");
    try
    {
      var result = await Task.Run(async () =>
        await CampaignRunner.RunAsync(
          _options.Seed,
          _options.DaysHours,
          quiet: true,
          drama: _options.Drama,
          story: false,
          progress: (done, total) =>
          {
            var pct = (int)(done * 100 / Math.Max(1, total));
            Dispatcher.UIThread.Post(() =>
            {
              _feedback.SetBusy($"Campaign {DurationArg.Format(done)} / {DurationArg.Format(total)} ({pct}%)");
              _feedback.SetStatus($"Simulating… {pct}%");
            });
          })).ConfigureAwait(true);

      var model = CampaignBriefingModel.From(result);
      Bind(model);
      subtitle.Text = model.SubtitleLine;
      _feedback.ClearBusy();
      _feedback.SetStatus(model.HashLine);
      _feedback.Flash($"Briefing ready — {model.LifeMomentHits} life moments in {model.MilestoneCount} beats");
    }
    catch (Exception ex)
    {
      _feedback.ClearBusy();
      _feedback.FlashError(ex.Message);
    }
  }

  void Bind(CampaignBriefingModel model)
  {
    _model = model;
    _map.SetMap(model.MapPoints, model.MapEdges);
    _feed.SetLines(model.Feed);
    _scorecard.SetRows(model.Scorecard, model.ScorecardTitle);
    _ledgers.SetPair("Ops", model.OpsCash, "Core", model.CoreCash, $"Never summed · {model.OpsNote} | {model.CoreNote}");
    _registry.SetRows(model.RegistryRows);
    _logistics.SetRows(model.LogisticsRows);
    _money.SetRows(model.MoneyRows);
    _agents.SetRows(model.AgentRows);
    _mega.SetRows(model.MegaRows);
    _curtain.Text = model.CurtainLine;
    _raw.Text = model.RawReport;
  }

  void OnStarSelected(string id)
  {
    if (_model?.HubDetails.TryGetValue(id, out var hub) == true)
    {
      _hubDetail.Text = $"{hub.Name} · {hub.Role}\n{hub.ProfileHint}";
      _hubDetail.Foreground = new SolidColorBrush(Color.Parse("#e8e8e8"));
    }
    else
    {
      _hubDetail.Text = id;
    }
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
          new TextBlock
          {
            Text = title,
            FontWeight = FontWeight.SemiBold,
            Foreground = BrandBrush,
          },
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
