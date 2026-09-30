using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.Agent;
using Novolis.Avalonia.Agent.Protocol;
using Novolis.Avalonia.GraphicalProfile;

namespace SinsOfACapitalismTycoon.Ui;

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
