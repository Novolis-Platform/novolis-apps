using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.Agent;
using Novolis.Avalonia.Agent.Protocol;
using Novolis.Avalonia.GraphicalProfile;

namespace SinsOfACapitalismTycoon.Ui;

/// <summary>ListBox row model for goods charters / standby.</summary>
internal sealed record CharterContractRow(
  string Title,
  string Detail,
  bool CanAccept,
  int Index);
