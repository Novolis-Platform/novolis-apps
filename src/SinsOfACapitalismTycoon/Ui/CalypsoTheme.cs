using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.Agent;
using Novolis.Avalonia.Agent.Protocol;
using Novolis.Avalonia.GraphicalProfile;

namespace SinsOfACapitalismTycoon.Ui;

internal static class CalypsoTheme
{
  public static void ApplyWindowChrome(Window window)
  {
    window.FontFamily = GraphicalProfile.BodyFont;
    GraphicalProfileBinding.Bind(
      window,
      Window.BackgroundProperty,
      GraphicalProfile.BackgroundResourceKey);
    GraphicalProfileBinding.Bind(
      window,
      Window.ForegroundProperty,
      GraphicalProfile.TextResourceKey);
  }

  public static Button MakeButton(string text, string agentId, CalypsoButtonKind kind)
  {
    var btn = new Button
    {
      Content = text,
      Padding = new Thickness(14, 7),
      Margin = new Thickness(0, 0, 6, 4),
      FontFamily = GraphicalProfile.BodyFont,
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
        btn.Background = GraphicalProfile.ActionBrush;
        btn.Foreground = GraphicalProfile.OnActionBrush;
        btn.BorderBrush = GraphicalProfile.AccentBrush;
        btn.BorderThickness = new Thickness(1);
        break;
      case CalypsoButtonKind.Danger:
        btn.Background = GraphicalProfile.RaisedBrush;
        btn.Foreground = GraphicalProfile.DangerBrush;
        btn.BorderBrush = GraphicalProfile.DangerBrush;
        btn.BorderThickness = new Thickness(1);
        break;
      case CalypsoButtonKind.Quiet:
        btn.Background = Brushes.Transparent;
        btn.Foreground = GraphicalProfile.MutedBrush;
        btn.BorderBrush = GraphicalProfile.BorderBrush;
        btn.BorderThickness = new Thickness(1);
        break;
      default:
        btn.Background = GraphicalProfile.RaisedBrush;
        btn.Foreground = GraphicalProfile.TextBrush;
        btn.BorderBrush = GraphicalProfile.BorderBrush;
        btn.BorderThickness = new Thickness(1);
        break;
    }
  }

  public static Border MetricChip(string label, string value, out TextBlock valueBlock)
  {
    valueBlock = new TextBlock
    {
      Text = value,
      FontFamily = GraphicalProfile.BodyFont,
      FontSize = 18,
      FontWeight = FontWeight.SemiBold,
      Foreground = GraphicalProfile.ActionBrush,
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
          Foreground = GraphicalProfile.MutedBrush,
          FontFamily = GraphicalProfile.BodyFont,
        },
        valueBlock,
      },
    };
    return new Border
    {
      Background = GraphicalProfile.RaisedBrush,
      BorderBrush = GraphicalProfile.BorderBrush,
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
      Background = GraphicalProfile.SurfaceBrush,
      BorderBrush = GraphicalProfile.BorderBrush,
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
            FontFamily = GraphicalProfile.BodyFont,
            FontWeight = FontWeight.SemiBold,
            FontSize = 15,
            Foreground = GraphicalProfile.ActionBrush,
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
      FontFamily = GraphicalProfile.BodyFont,
      FontSize = 12,
      Foreground = GraphicalProfile.TextBrush,
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
      badgeBg = GraphicalProfile.RaisedBrush;
      badgeFg = GraphicalProfile.MutedBrush;
    }
    else if (band.Equals("Fat", StringComparison.OrdinalIgnoreCase))
    {
      badgeBg = GraphicalProfile.RaisedBrush;
      badgeFg = GraphicalProfile.ActionBrush;
    }
    else if (band.Equals("Thin", StringComparison.OrdinalIgnoreCase))
    {
      badgeBg = GraphicalProfile.SurfaceBrush;
      badgeFg = GraphicalProfile.MutedBrush;
    }
    else if (actionable)
    {
      badgeBg = GraphicalProfile.ActionSoftBrush;
      badgeFg = GraphicalProfile.ActionSoftBrush;
    }
    else
    {
      badgeBg = GraphicalProfile.RaisedBrush;
      badgeFg = GraphicalProfile.MutedBrush;
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
      Background = GraphicalProfile.RaisedBrush,
      BorderBrush = GraphicalProfile.BorderBrush,
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
                FontFamily = GraphicalProfile.BodyFont,
                FontWeight = FontWeight.SemiBold,
                FontSize = 13,
                Foreground = GraphicalProfile.TextBrush,
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
            Foreground = GraphicalProfile.AccentBrush,
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
              new GradientStop(GraphicalProfile.Background, 0),
              new GradientStop(GraphicalProfile.Surface, 0.5),
              new GradientStop(GraphicalProfile.Raised, 1),
            },
          },
        },
        map,
        vignette,
      },
    };
  }
}
