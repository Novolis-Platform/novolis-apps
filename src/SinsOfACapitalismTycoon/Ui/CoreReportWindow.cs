using Novolis.Avalonia.GraphicalProfile;
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
