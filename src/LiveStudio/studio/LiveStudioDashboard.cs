using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Novolis.Audio.Live.Protocol.Dto;
using Novolis.Audio.Live.Visuals;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Avalonia.Live;

namespace LiveStudio;

internal sealed class LiveStudioDashboard : Grid
{
    private static IBrush SurfaceBrush => GraphicalProfile.SurfaceBrush;
    private static IBrush BorderBrush => GraphicalProfile.BorderBrush;
    private static IBrush TextBrush => GraphicalProfile.TextBrush;
    private static IBrush MutedBrush => GraphicalProfile.MutedBrush;
    private static IBrush AccentBrush => GraphicalProfile.AccentFillBrush;
    private static IBrush SuccessBrush => GraphicalProfile.ActionSoftBrush;
    private static IBrush WarningBrush => GraphicalProfile.WarningBrush;
    private static IBrush ErrorBrush => GraphicalProfile.DangerBrush;
    private static IBrush AccentPaleBrush => GraphicalProfile.RaisedBrush;
    private static IBrush SuccessPaleBrush => GraphicalProfile.RaisedBrush;
    private static IBrush WarningPaleBrush => GraphicalProfile.RaisedBrush;
    private static IBrush NeutralPaleBrush => GraphicalProfile.SurfaceBrush;
    private static IBrush BeatPulseBrush => GraphicalProfile.ActionSoftBrush;

    private readonly TextBlock _connectionStatus = new();
    private readonly TextBlock _activityStatus = new();
    private readonly TextBlock _currentPreset = new();
    private readonly TextBlock _nextPreset = new();
    private readonly TextBlock _snapshotSummary = new();
    private readonly TextBlock _timingSummary = new();
    private readonly TextBlock _swapSummary = new();
    private readonly TextBlock _errorMessage = new();
    private readonly TextBlock _diagnosticSummary = new();
    private readonly Border _beatPulse = new();
    private readonly StackPanel _diagnosticsList = new();
    private readonly StackPanel _presetList = new();
    private readonly LiveProgramGraphView _graph = new();
    private int _lastBeatFloor = -1;

    public LiveStudioDashboard()
    {
        Background = GraphicalProfile.BackgroundBrush;
        RowDefinitions = new RowDefinitions("Auto,Auto,*");
        ColumnDefinitions = new ColumnDefinitions("2*,3*");
        Margin = new Thickness(24);

        _beatPulse.Width = 28;
        _beatPulse.Height = 28;
        _beatPulse.CornerRadius = new CornerRadius(999);
        _beatPulse.Background = NeutralPaleBrush;
        _beatPulse.Child = new TextBlock
        {
            Text = "♪",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = SuccessBrush,
            FontSize = 14,
            FontWeight = FontWeight.Bold,
        };

        Children.Add(BuildHeader());
        Children.Add(BuildStatusStrip());
        Children.Add(BuildBody());
    }

    public event Action<LiveProgramPreset>? PresetSelected;

    public void Bind(LiveStudioState state)
    {
        _connectionStatus.Text = state.ConnectionStatus;
        _activityStatus.Text = state.ActivityStatus;
        _currentPreset.Text = $"Now: {state.CurrentPresetName}";
        _nextPreset.Text = state.DemoSequenceRunning && !string.IsNullOrWhiteSpace(state.NextPresetName)
            ? $"Demo next: {state.NextPresetName}"
            : string.IsNullOrWhiteSpace(state.NextPresetName)
                ? "Click a preset to swap live"
                : $"Next: {state.NextPresetName}";

        if (state.Snapshot is null)
        {
            _snapshotSummary.Text = "Waiting for host transport...";
            _timingSummary.Text = "Beat — | Bar — | Phrase —";
            _swapSummary.Text = "Audio starts when Pulse Bloom loads.";
            _beatPulse.Background = NeutralPaleBrush;
        }
        else
        {
            _snapshotSummary.Text = state.Snapshot.ActiveProgramId is null
                ? "No active program yet."
                : $"Program v{state.Snapshot.ActiveVersion} @ {state.Snapshot.Bpm:0.###} BPM";
            _timingSummary.Text =
                $"Beat {state.Snapshot.Beat:0.###}  ·  Bar {state.Snapshot.Bar}  ·  Phrase {state.Snapshot.Phrase}";
            _swapSummary.Text = state.Snapshot.PendingProgramId is null
                ? "No queued swap."
                : $"Queued swap via {state.Snapshot.PendingSwapPolicy}";

            var beatFloor = (int)Math.Floor((double)state.Snapshot.Beat);
            if (beatFloor != _lastBeatFloor)
            {
                _lastBeatFloor = beatFloor;
                PulseBeat();
            }
        }

        _errorMessage.Text = string.IsNullOrWhiteSpace(state.ErrorMessage) ? string.Empty : state.ErrorMessage!;
        _errorMessage.IsVisible = !string.IsNullOrWhiteSpace(state.ErrorMessage);
        _errorMessage.MaxHeight = 72;

        _diagnosticSummary.Text = state.Diagnostics.Count == 0
            ? "Diagnostics: clean compile."
            : $"Diagnostics: {state.Diagnostics.Count} item(s)";

        _diagnosticsList.Children.Clear();
        if (state.Diagnostics.Count == 0)
        {
            _diagnosticsList.Children.Add(BuildEmptyLine("No diagnostics to show."));
        }
        else
        {
            foreach (var diagnostic in state.Diagnostics)
                _diagnosticsList.Children.Add(BuildDiagnosticRow(diagnostic));
        }

        _presetList.Children.Clear();
        foreach (var preset in state.Presets)
            _presetList.Children.Add(BuildPresetRow(preset, preset.Name == state.CurrentPresetName, preset.Name == state.NextPresetName));

        _graph.Bind(state.Graph);
    }

    private void PulseBeat()
    {
        _beatPulse.Background = BeatPulseBrush;
        DispatcherTimer.RunOnce(
            () => _beatPulse.Background = NeutralPaleBrush,
            TimeSpan.FromMilliseconds(120));
    }

    private Control BuildHeader()
    {
        var header = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(0, 0, 0, 16),
        };

        header.Children.Add(new TextBlock
        {
            Text = "Novolis Audio Live",
            FontSize = 28,
            FontWeight = FontWeight.SemiBold,
            Foreground = TextBrush,
        });

        header.Children.Add(new TextBlock
        {
            Text = "Typed live coding with queued swaps — the host plays oscillators as soon as a program lands.",
            FontSize = 15,
            Foreground = MutedBrush,
            TextWrapping = TextWrapping.Wrap,
        });

        header.Children.Add(new TextBlock
        {
            Text = "Demos load as editable Live DSL in the editor. F5 compiles. Open Graph / Piano / Interpretation windows.",
            FontSize = 13,
            Foreground = MutedBrush,
            TextWrapping = TextWrapping.Wrap,
        });

        Grid.SetRow(header, 0);
        Grid.SetColumnSpan(header, 2);
        return header;
    }

    private Control BuildStatusStrip()
    {
        var strip = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto"),
            Margin = new Thickness(0, 0, 0, 16),
        };

        strip.Children.Add(_beatPulse);
        Grid.SetColumn(_beatPulse, 0);

        var chips = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto"),
            Margin = new Thickness(12, 0, 0, 0),
        };
        chips.Children.Add(CreateChip(_connectionStatus, AccentPaleBrush, AccentBrush, 0));
        chips.Children.Add(CreateChip(_activityStatus, SuccessPaleBrush, SuccessBrush, 1));
        chips.Children.Add(CreateChip(_currentPreset, WarningPaleBrush, WarningBrush, 2));
        chips.Children.Add(CreateChip(_nextPreset, NeutralPaleBrush, TextBrush, 3));
        Grid.SetColumn(chips, 1);
        strip.Children.Add(chips);

        Grid.SetRow(strip, 1);
        Grid.SetColumnSpan(strip, 2);
        return strip;
    }

    private Control BuildBody()
    {
        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("2*,3*"),
        };

        var left = new StackPanel { Spacing = 16 };
        // Presets first — that's the wow path for the live demo.
        left.Children.Add(BuildPresetCard());
        left.Children.Add(BuildTransportCard());
        left.Children.Add(BuildDiagnosticsCard());

        var leftScroll = new ScrollViewer
        {
            Content = left,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };

        var right = new Border
        {
            Background = SurfaceBrush,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(16),
            Child = BuildGraphCard(),
        };

        body.Children.Add(leftScroll);
        body.Children.Add(right);
        Grid.SetColumn(right, 1);

        Grid.SetRow(body, 2);
        Grid.SetColumnSpan(body, 2);
        return body;
    }

    private Control BuildTransportCard()
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(BuildCardTitle("Transport"));
        panel.Children.Add(_snapshotSummary);

        _timingSummary.FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace");
        _timingSummary.FontSize = 14;
        _timingSummary.Foreground = AccentBrush;
        _timingSummary.FontWeight = FontWeight.SemiBold;
        panel.Children.Add(_timingSummary);

        panel.Children.Add(_swapSummary);
        _errorMessage.Foreground = ErrorBrush;
        _errorMessage.TextWrapping = TextWrapping.Wrap;
        panel.Children.Add(_errorMessage);

        return CreateCard(panel);
    }

    private Control BuildDiagnosticsCard()
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(BuildCardTitle("Compile feedback"));
        panel.Children.Add(_diagnosticSummary);
        panel.Children.Add(_diagnosticsList);
        return CreateCard(panel);
    }

    private Control BuildPresetCard()
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(BuildCardTitle("Demos — click to load into editor & play"));
        panel.Children.Add(new TextBlock
        {
            Text = "Each demo is real Live DSL source. Clicking replaces the editor buffer, then compiles.",
            Foreground = MutedBrush,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        });

        _presetList.Spacing = 8;
        panel.Children.Add(_presetList);

        return CreateCard(panel);
    }

    private Control BuildGraphCard()
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(BuildCardTitle("Program graph"));
        panel.Children.Add(_graph);
        return panel;
    }

    private static Border CreateCard(Control content) =>
        new()
        {
            Background = SurfaceBrush,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(16),
            Child = content,
        };

    private static Control BuildCardTitle(string text) =>
        new TextBlock
        {
            Text = text,
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            Foreground = TextBrush,
        };

    private static Control CreateChip(TextBlock textBlock, IBrush background, IBrush foreground, int column)
    {
        var chip = new Border
        {
            Background = background,
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(12, 8),
            Margin = new Thickness(column == 0 ? 0 : 8, 0, 0, 0),
            Child = textBlock,
        };

        textBlock.Foreground = foreground;
        textBlock.FontSize = 13;
        textBlock.TextWrapping = TextWrapping.NoWrap;
        textBlock.MaxWidth = 280;
        textBlock.TextTrimming = TextTrimming.CharacterEllipsis;

        Grid.SetColumn(chip, column);
        return chip;
    }

    private static Control BuildEmptyLine(string text) =>
        new TextBlock
        {
            Text = text,
            Foreground = MutedBrush,
            FontStyle = FontStyle.Italic,
        };

    private static Border BuildDiagnosticRow(LiveDiagnosticDto diagnostic)
    {
        var (brush, title) = diagnostic.Severity switch
        {
            Novolis.Audio.Live.LiveDiagnosticSeverity.Error => (ErrorBrush, "Error"),
            Novolis.Audio.Live.LiveDiagnosticSeverity.Warning => (WarningBrush, "Warning"),
            _ => (AccentBrush, "Info"),
        };

        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock
        {
            Text = $"{title} {diagnostic.Code}",
            FontWeight = FontWeight.SemiBold,
            Foreground = brush,
        });
        panel.Children.Add(new TextBlock
        {
            Text = diagnostic.Message,
            Foreground = TextBrush,
            TextWrapping = TextWrapping.Wrap,
        });

        if (!string.IsNullOrWhiteSpace(diagnostic.Location))
        {
            panel.Children.Add(new TextBlock
            {
                Text = diagnostic.Location,
                Foreground = MutedBrush,
                FontSize = 12,
            });
        }

        return new Border
        {
            Background = GraphicalProfile.RaisedBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12),
            Child = panel,
        };
    }

    private Border BuildPresetRow(LiveProgramPreset preset, bool isCurrent, bool isNext)
    {
        var accent = isCurrent
            ? SuccessBrush
            : isNext
                ? WarningBrush
                : BorderBrush;

        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock
        {
            Text = isCurrent ? $"▶ {preset.Name}" : preset.Name,
            Foreground = TextBrush,
            FontWeight = FontWeight.SemiBold,
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"{preset.Description} · {preset.SwapPolicy}",
            Foreground = MutedBrush,
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(new TextBlock
        {
            Text = isCurrent ? "Playing now — click another preset to swap" : "Click to compile & play",
            Foreground = isCurrent ? SuccessBrush : AccentBrush,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
        });

        var border = new Border
        {
            Background = isCurrent ? GraphicalProfile.RaisedBrush : GraphicalProfile.SurfaceBrush,
            BorderBrush = accent,
            BorderThickness = new Thickness(isCurrent || isNext ? 2 : 1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = panel,
        };

        border.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
            {
                PresetSelected?.Invoke(preset);
                e.Handled = true;
            }
        };

        return border;
    }
}
