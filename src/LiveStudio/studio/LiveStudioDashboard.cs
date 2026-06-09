using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Audio.Live.Protocol.Dto;
using Novolis.Audio.Live.Visuals;
using Novolis.Avalonia.Live;

namespace LiveStudio;

internal sealed class LiveStudioDashboard : Grid
{
    private static readonly SolidColorBrush SurfaceBrush = new(Color.Parse("#FFFFFF"));
    private static readonly SolidColorBrush BorderBrush = new(Color.Parse("#D6DBE5"));
    private static readonly SolidColorBrush TextBrush = new(Color.Parse("#0F172A"));
    private static readonly SolidColorBrush MutedBrush = new(Color.Parse("#475569"));
    private static readonly SolidColorBrush AccentBrush = new(Color.Parse("#1D4ED8"));
    private static readonly SolidColorBrush SuccessBrush = new(Color.Parse("#047857"));
    private static readonly SolidColorBrush WarningBrush = new(Color.Parse("#B45309"));
    private static readonly SolidColorBrush ErrorBrush = new(Color.Parse("#B91C1C"));
    private static readonly SolidColorBrush AccentPaleBrush = new(Color.Parse("#DBEAFE"));
    private static readonly SolidColorBrush SuccessPaleBrush = new(Color.Parse("#D1FAE5"));
    private static readonly SolidColorBrush WarningPaleBrush = new(Color.Parse("#FEF3C7"));
    private static readonly SolidColorBrush NeutralPaleBrush = new(Color.Parse("#E2E8F0"));

    private readonly TextBlock _connectionStatus = new();
    private readonly TextBlock _activityStatus = new();
    private readonly TextBlock _currentPreset = new();
    private readonly TextBlock _nextPreset = new();
    private readonly TextBlock _snapshotSummary = new();
    private readonly TextBlock _swapSummary = new();
    private readonly TextBlock _errorMessage = new();
    private readonly TextBlock _diagnosticSummary = new();
    private readonly StackPanel _diagnosticsList = new();
    private readonly StackPanel _presetList = new();
    private readonly LiveProgramGraphView _graph = new();

    public LiveStudioDashboard()
    {
        Background = new SolidColorBrush(Color.Parse("#F4F7FB"));
        RowDefinitions = new RowDefinitions("Auto,Auto,*");
        ColumnDefinitions = new ColumnDefinitions("2*,3*");
        Margin = new Thickness(24);

        Children.Add(BuildHeader());
        Children.Add(BuildStatusStrip());
        Children.Add(BuildBody());
    }

    public void Bind(LiveStudioState state)
    {
        _connectionStatus.Text = state.ConnectionStatus;
        _activityStatus.Text = state.ActivityStatus;
        _currentPreset.Text = $"Now running: {state.CurrentPresetName}";
        _nextPreset.Text = string.IsNullOrWhiteSpace(state.NextPresetName)
            ? "Next preset: none queued"
            : $"Next preset: {state.NextPresetName}";

        if (state.Snapshot is null)
        {
            _snapshotSummary.Text = "No transport snapshot yet.";
            _swapSummary.Text = "Waiting for the host to publish timing.";
        }
        else
        {
            _snapshotSummary.Text = state.Snapshot.ActiveProgramId is null
                ? "No active program yet."
                : $"Program {state.Snapshot.ActiveProgramId} v{state.Snapshot.ActiveVersion} @ {state.Snapshot.Bpm:0.###} BPM";
            _swapSummary.Text = state.Snapshot.PendingProgramId is null
                ? "No queued swap."
                : $"Queued {state.Snapshot.PendingProgramId} via {state.Snapshot.PendingSwapPolicy}";
        }

        _errorMessage.Text = string.IsNullOrWhiteSpace(state.ErrorMessage) ? string.Empty : state.ErrorMessage!;
        _errorMessage.IsVisible = !string.IsNullOrWhiteSpace(state.ErrorMessage);

        _diagnosticSummary.Text = state.Diagnostics.Count == 0
            ? "Diagnostics: no compile issues."
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

    private Control BuildHeader()
    {
        var header = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(0, 0, 0, 16),
        };

        header.Children.Add(new TextBlock
        {
            Text = "Novolis Audio Live Studio",
            FontSize = 28,
            FontWeight = FontWeight.SemiBold,
            Foreground = TextBrush,
        });

        header.Children.Add(new TextBlock
        {
            Text = "Typed live coding, queued swaps, and visual projections driven by the same musical model.",
            FontSize = 15,
            Foreground = MutedBrush,
            TextWrapping = TextWrapping.Wrap,
        });

        header.Children.Add(new TextBlock
        {
            Text = "Start the host, compile a preset, and watch the graph and transport update together.",
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
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto"),
            Margin = new Thickness(0, 0, 0, 16),
        };

        strip.Children.Add(CreateChip(_connectionStatus, AccentPaleBrush, AccentBrush, 0));
        strip.Children.Add(CreateChip(_activityStatus, SuccessPaleBrush, SuccessBrush, 1));
        strip.Children.Add(CreateChip(_currentPreset, WarningPaleBrush, WarningBrush, 2));
        strip.Children.Add(CreateChip(_nextPreset, NeutralPaleBrush, TextBrush, 3));

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
        left.Children.Add(BuildTransportCard());
        left.Children.Add(BuildDiagnosticsCard());
        left.Children.Add(BuildPresetCard());

        var right = new Border
        {
            Background = SurfaceBrush,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(16),
            Child = BuildGraphCard(),
        };

        body.Children.Add(left);
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
        panel.Children.Add(_swapSummary);
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
        panel.Children.Add(BuildCardTitle("Showcase presets"));

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
            Background = new SolidColorBrush(Color.Parse("#F8FAFC")),
            BorderBrush = new SolidColorBrush(Color.Parse("#E2E8F0")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12),
            Child = panel,
        };
    }

    private static Border BuildPresetRow(LiveProgramPreset preset, bool isCurrent, bool isNext)
    {
        var accent = isCurrent
            ? SuccessBrush
            : isNext
                ? WarningBrush
                : BorderBrush;

        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock
        {
            Text = preset.Name,
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
            Text = $"Version {preset.Version} · delay {preset.DelayBeforeCompile.TotalSeconds:0.#}s",
            Foreground = MutedBrush,
            FontSize = 12,
        });

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#FAFBFC")),
            BorderBrush = accent,
            BorderThickness = new Thickness(isCurrent || isNext ? 2 : 1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12),
            Child = panel,
        };
    }
}
