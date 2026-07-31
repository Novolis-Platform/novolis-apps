using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.Agent;
using Novolis.Avalonia.Agent.Protocol;

namespace SinsOfACapitalismTycoon.Ui;

/// <summary>Tramp freighter bridge tokens — navy / teal atmosphere, copper–amber accent.</summary>
internal static class CalypsoPalette
{
  public static readonly Color Window = Color.Parse("#071018");
  public static readonly Color Panel = Color.Parse("#0e1824");
  public static readonly Color PanelRaised = Color.Parse("#152434");
  public static readonly Color Accent = Color.Parse("#d4a017");
  public static readonly Color AccentSoft = Color.Parse("#c8b060");
  public static readonly Color Body = Color.Parse("#e8e4d8");
  public static readonly Color Muted = Color.Parse("#8a96a8");
  public static readonly Color Success = Color.Parse("#6ecf8e");
  public static readonly Color Danger = Color.Parse("#c45c4a");
  public static readonly Color PrimaryFace = Color.Parse("#1a2838");
  public static readonly Color MapField = Color.Parse("#0a1524");

  public static readonly IBrush WindowBrush = new SolidColorBrush(Window);
  public static readonly IBrush PanelBrush = new SolidColorBrush(Panel);
  public static readonly IBrush PanelRaisedBrush = new SolidColorBrush(PanelRaised);
  public static readonly IBrush AccentBrush = new SolidColorBrush(Accent);
  public static readonly IBrush AccentSoftBrush = new SolidColorBrush(AccentSoft);
  public static readonly IBrush BodyBrush = new SolidColorBrush(Body);
  public static readonly IBrush MutedBrush = new SolidColorBrush(Muted);
  public static readonly IBrush SuccessBrush = new SolidColorBrush(Success);
  public static readonly IBrush DangerBrush = new SolidColorBrush(Danger);
  public static readonly IBrush MapFieldBrush = new SolidColorBrush(MapField);

  /// <summary>Display face for brand / voyage (serif tramp chart energy).</summary>
  public static readonly FontFamily DisplayFont =
    new("Georgia, Palatino Linotype, Book Antiqua, Times New Roman, serif");

  /// <summary>Readable UI body — not Inter.</summary>
  public static readonly FontFamily BodyFont =
    new("Segoe UI, Candara, Calibri, sans-serif");

  public static readonly FontFamily MonoFont =
    new("Consolas, Cascadia Mono, Courier New, monospace");
}

internal enum CalypsoButtonKind
{
  Primary,
  Secondary,
  Danger,
  Quiet,
}

internal static class CalypsoTheme
{
  public static void ApplyWindowChrome(Window window)
  {
    window.Background = CalypsoPalette.WindowBrush;
    window.FontFamily = CalypsoPalette.BodyFont;
    window.Foreground = CalypsoPalette.BodyBrush;
  }

  public static Button MakeButton(string text, string agentId, CalypsoButtonKind kind)
  {
    var btn = new Button
    {
      Content = text,
      Padding = new Thickness(14, 7),
      Margin = new Thickness(0, 0, 6, 4),
      FontFamily = CalypsoPalette.BodyFont,
      FontSize = kind == CalypsoButtonKind.Primary ? 13 : 12,
      FontWeight = kind == CalypsoButtonKind.Primary ? FontWeight.SemiBold : FontWeight.Normal,
      CornerRadius = new CornerRadius(3),
      Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
    };
    StyleButton(btn, kind);
    AgentProperties.SetId(btn, agentId, AgentRoleNames.Button);
    return btn;
  }

  public static void StyleButton(Button btn, CalypsoButtonKind kind)
  {
    switch (kind)
    {
      case CalypsoButtonKind.Primary:
        btn.Background = CalypsoPalette.AccentBrush;
        btn.Foreground = new SolidColorBrush(CalypsoPalette.PrimaryFace);
        btn.BorderBrush = CalypsoPalette.AccentSoftBrush;
        btn.BorderThickness = new Thickness(1);
        break;
      case CalypsoButtonKind.Danger:
        btn.Background = new SolidColorBrush(Color.Parse("#3a2220"));
        btn.Foreground = CalypsoPalette.DangerBrush;
        btn.BorderBrush = CalypsoPalette.DangerBrush;
        btn.BorderThickness = new Thickness(1);
        break;
      case CalypsoButtonKind.Quiet:
        btn.Background = Brushes.Transparent;
        btn.Foreground = CalypsoPalette.MutedBrush;
        btn.BorderBrush = new SolidColorBrush(Color.Parse("#2a3848"));
        btn.BorderThickness = new Thickness(1);
        break;
      default:
        btn.Background = CalypsoPalette.PanelRaisedBrush;
        btn.Foreground = CalypsoPalette.BodyBrush;
        btn.BorderBrush = new SolidColorBrush(Color.Parse("#2a3848"));
        btn.BorderThickness = new Thickness(1);
        break;
    }
  }

  public static Border MetricChip(string label, string value, out TextBlock valueBlock)
  {
    valueBlock = new TextBlock
    {
      Text = value,
      FontFamily = CalypsoPalette.DisplayFont,
      FontSize = 18,
      FontWeight = FontWeight.SemiBold,
      Foreground = CalypsoPalette.AccentBrush,
    };
    var stack = new StackPanel
    {
      Spacing = 2,
      Children =
      {
        new TextBlock
        {
          Text = label,
          FontSize = 10,
          Foreground = CalypsoPalette.MutedBrush,
          FontFamily = CalypsoPalette.BodyFont,
        },
        valueBlock,
      },
    };
    return new Border
    {
      Background = CalypsoPalette.PanelRaisedBrush,
      BorderBrush = new SolidColorBrush(Color.Parse("#2a3848")),
      BorderThickness = new Thickness(1),
      CornerRadius = new CornerRadius(4),
      Padding = new Thickness(10, 6),
      Margin = new Thickness(0, 0, 8, 4),
      Child = stack,
    };
  }

  public static Border Section(string title, Control child) =>
    new()
    {
      Background = CalypsoPalette.PanelBrush,
      BorderBrush = new SolidColorBrush(Color.Parse("#1e2c3c")),
      BorderThickness = new Thickness(1),
      Padding = new Thickness(12, 10),
      CornerRadius = new CornerRadius(6),
      Child = new StackPanel
      {
        Spacing = 8,
        Children =
        {
          new TextBlock
          {
            Text = title,
            FontFamily = CalypsoPalette.DisplayFont,
            FontWeight = FontWeight.SemiBold,
            FontSize = 15,
            Foreground = CalypsoPalette.AccentBrush,
          },
          child,
        },
      },
    };

  public static IDataTemplate SpotContractTemplate() =>
    new FuncDataTemplate<SpotContractRow>((row, _) =>
      BuildContractRow(
        row.Title, row.Detail, row.AtDock || row.IsRumor, row.Badge, row.Band, row.IsWait), true);

  public static IDataTemplate CharterContractTemplate() =>
    new FuncDataTemplate<CharterContractRow>((row, _) =>
      BuildContractRow(row.Title, row.Detail, row.CanAccept, row.CanAccept ? "TAKE" : "HOLD"), true);

  public static IDataTemplate StringRowTemplate() =>
    new FuncDataTemplate<string>((s, _) => new TextBlock
    {
      Text = s,
      TextWrapping = TextWrapping.Wrap,
      FontFamily = CalypsoPalette.BodyFont,
      FontSize = 12,
      Foreground = CalypsoPalette.BodyBrush,
      Margin = new Thickness(4, 4),
    }, true);

  static Control BuildContractRow(
    string title,
    string detail,
    bool actionable,
    string badgeText,
    string band = "",
    bool isWait = false)
  {
    IBrush badgeBg;
    IBrush badgeFg;
    if (isWait)
    {
      badgeBg = CalypsoPalette.PanelRaisedBrush;
      badgeFg = CalypsoPalette.MutedBrush;
    }
    else if (band.Equals("Fat", StringComparison.OrdinalIgnoreCase))
    {
      badgeBg = new SolidColorBrush(Color.Parse("#3a3020"));
      badgeFg = CalypsoPalette.AccentBrush;
    }
    else if (band.Equals("Thin", StringComparison.OrdinalIgnoreCase))
    {
      badgeBg = new SolidColorBrush(Color.Parse("#2a3038"));
      badgeFg = CalypsoPalette.MutedBrush;
    }
    else if (actionable)
    {
      badgeBg = new SolidColorBrush(Color.Parse("#2a4030"));
      badgeFg = CalypsoPalette.SuccessBrush;
    }
    else
    {
      badgeBg = CalypsoPalette.PanelRaisedBrush;
      badgeFg = CalypsoPalette.MutedBrush;
    }

    var badge = new Border
    {
      Background = badgeBg,
      CornerRadius = new CornerRadius(3),
      Padding = new Thickness(6, 2),
      Child = new TextBlock
      {
        Text = badgeText,
        FontSize = 9,
        FontWeight = FontWeight.Bold,
        Foreground = badgeFg,
      },
    };

    badge.SetValue(DockPanel.DockProperty, Dock.Left);
    return new Border
    {
      Background = CalypsoPalette.PanelRaisedBrush,
      BorderBrush = new SolidColorBrush(Color.Parse("#2a3848")),
      BorderThickness = new Thickness(1),
      CornerRadius = new CornerRadius(4),
      Padding = new Thickness(10, 8),
      Margin = new Thickness(0, 0, 0, 6),
      Child = new StackPanel
      {
        Spacing = 4,
        Children =
        {
          new DockPanel
          {
            LastChildFill = true,
            Children =
            {
              badge,
              new TextBlock
              {
                Text = title,
                FontFamily = CalypsoPalette.BodyFont,
                FontWeight = FontWeight.SemiBold,
                FontSize = 13,
                Foreground = CalypsoPalette.BodyBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
              },
            },
          },
          new TextBlock
          {
            Text = detail,
            FontSize = 11,
            Foreground = CalypsoPalette.AccentSoftBrush,
            TextWrapping = TextWrapping.Wrap,
          },
        },
      },
    };
  }

  public static Control MapAtmosphereHost(Control map)
  {
    var vignette = new Border
    {
      IsHitTestVisible = false,
      Background = new RadialGradientBrush
      {
        GradientOrigin = new RelativePoint(0.5, 0.45, RelativeUnit.Relative),
        Center = new RelativePoint(0.5, 0.45, RelativeUnit.Relative),
        RadiusX = new RelativeScalar(0.75, RelativeUnit.Relative),
        RadiusY = new RelativeScalar(0.75, RelativeUnit.Relative),
        GradientStops =
        {
          new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.55),
          new GradientStop(Color.FromArgb(140, 4, 10, 18), 1),
        },
      },
    };

    return new Grid
    {
      Children =
      {
        new Border
        {
          Background = new LinearGradientBrush
          {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
              new GradientStop(Color.Parse("#081420"), 0),
              new GradientStop(Color.Parse("#0c1c2e"), 0.5),
              new GradientStop(Color.Parse("#0a1524"), 1),
            },
          },
        },
        map,
        vignette,
      },
    };
  }
}

/// <summary>ListBox row model for spot freight / berth offers.</summary>
internal sealed record SpotContractRow(
  string Title,
  string Detail,
  bool AtDock,
  int Index,
  string Badge = "AT DOCK",
  bool IsRumor = false,
  bool IsWait = false,
  string Band = "");

/// <summary>ListBox row model for goods charters / standby.</summary>
internal sealed record CharterContractRow(
  string Title,
  string Detail,
  bool CanAccept,
  int Index);
