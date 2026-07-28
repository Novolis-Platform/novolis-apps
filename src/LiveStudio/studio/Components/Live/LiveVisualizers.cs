using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Novolis.Audio.Live.Visuals;

namespace LiveStudio.Components.Live;

/// <summary>Contract for a Live visualizer panel (move to Novolis.Avalonia.Live).</summary>
public interface ILiveVisualizer
{
    string Title { get; }

    Control View { get; }

    void Bind(LiveVisualizerModel model);
}

public sealed record LiveVisualizerModel(
    LiveGraphNode? Graph,
    decimal Beat,
    int Bar,
    int Phrase,
    decimal Bpm,
    string? ActivePreset,
    string? SourceExcerpt);

/// <summary>Tree interpretation of the compiled program graph.</summary>
public sealed class LiveProgramGraphVisualizer : ILiveVisualizer
{
    readonly Novolis.Avalonia.Live.LiveProgramGraphView _graph = new();
    readonly TextBlock _caption = new()
    {
        Text = "Compiled program structure (from your editor buffer).",
        Foreground = new SolidColorBrush(Color.Parse("#64748B")),
        FontSize = 12,
        Margin = new Thickness(0, 0, 0, 8),
        TextWrapping = TextWrapping.Wrap,
    };

    readonly StackPanel _root;

    public LiveProgramGraphVisualizer()
    {
        _root = new StackPanel { Spacing = 4 };
        _root.Children.Add(_caption);
        _root.Children.Add(_graph);
    }

    public string Title => "Program graph";
    public Control View => _root;

    public void Bind(LiveVisualizerModel model) => _graph.Bind(model.Graph);
}

/// <summary>Piano-roll style lanes derived from the Live graph interpretation.</summary>
public sealed class LivePianoRollVisualizer : ILiveVisualizer
{
    readonly Canvas _canvas = new() { Height = 220, Background = new SolidColorBrush(Color.Parse("#0F172A")) };
    readonly TextBlock _hint = new()
    {
        Foreground = new SolidColorBrush(Color.Parse("#94A3B8")),
        FontSize = 12,
        Margin = new Thickness(0, 0, 0, 8),
        TextWrapping = TextWrapping.Wrap,
    };

    readonly StackPanel _root;
    LiveVisualizerModel _model = new(null, 0, 1, 1, 120, null, null);

    public LivePianoRollVisualizer()
    {
        _root = new StackPanel { Spacing = 4 };
        _root.Children.Add(_hint);
        _root.Children.Add(new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#334155")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = _canvas,
            ClipToBounds = true,
        });
        _canvas.SizeChanged += (_, _) => Redraw();
    }

    public string Title => "Piano roll";
    public Control View => _root;

    public void Bind(LiveVisualizerModel model)
    {
        _model = model;
        _hint.Text = model.Graph is null
            ? "Compile a program to interpret note lanes."
            : $"Interpreting {(model.ActivePreset ?? "buffer")} @ {model.Bpm:0} BPM — playhead beat {model.Beat:0.##}";
        Redraw();
    }

    void Redraw()
    {
        _canvas.Children.Clear();
        var width = Math.Max(_canvas.Bounds.Width, 400);
        var height = _canvas.Height;
        _canvas.Width = width;

        // Grid
        for (var i = 0; i <= 16; i++)
        {
            var x = i / 16.0 * width;
            _canvas.Children.Add(new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Point(x, 0),
                EndPoint = new Point(x, height),
                Stroke = new SolidColorBrush(Color.Parse(i % 4 == 0 ? "#475569" : "#1E293B")),
                StrokeThickness = i % 4 == 0 ? 1.5 : 1,
            });
        }

        var lanes = FlattenLanes(_model.Graph).Take(8).ToList();
        if (lanes.Count == 0)
            return;

        var laneHeight = height / lanes.Count;
        for (var i = 0; i < lanes.Count; i++)
        {
            var lane = lanes[i];
            var y = i * laneHeight + 4;
            _canvas.Children.Add(new TextBlock
            {
                Text = lane.Label,
                Foreground = new SolidColorBrush(Color.Parse("#CBD5E1")),
                FontSize = 11,
                Margin = new Thickness(6, y, 0, 0),
            });

            foreach (var note in lane.Notes)
            {
                var x = note.StartBeat / 4.0 * width; // show ~1 bar
                var w = Math.Max(6, note.DurationBeats / 4.0 * width);
                _canvas.Children.Add(new Border
                {
                    Width = w,
                    Height = Math.Max(10, laneHeight - 14),
                    Background = new SolidColorBrush(Color.Parse(lane.Color)),
                    CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(x, y + 16, 0, 0),
                });
            }
        }

        // Playhead
        var loopBeat = (double)(_model.Beat % 4m);
        var playX = loopBeat / 4.0 * width;
        _canvas.Children.Add(new Avalonia.Controls.Shapes.Line
        {
            StartPoint = new Point(playX, 0),
            EndPoint = new Point(playX, height),
            Stroke = new SolidColorBrush(Color.Parse("#22C55E")),
            StrokeThickness = 2,
        });
    }

    static IEnumerable<RollLane> FlattenLanes(LiveGraphNode? root)
    {
        if (root is null)
            yield break;

        var colors = new[] { "#38BDF8", "#A78BFA", "#F472B6", "#34D399", "#FBBF24" };
        var colorIndex = 0;
        foreach (var child in Walk(root))
        {
            var isTrack = child.Label.Contains("·", StringComparison.Ordinal) && child.Label.Contains("ch ", StringComparison.Ordinal);
            var isNote = child.Label.StartsWith("Note ", StringComparison.Ordinal);
            if (!isTrack && !isNote)
                continue;

            var notes = isNote
                ? new[] { new RollNote(0, 1) }
                : new[] { new RollNote(0, 1), new RollNote(1, 1), new RollNote(2, 0.75) };

            yield return new RollLane(
                child.Label.Split('·')[0].Trim(),
                colors[colorIndex++ % colors.Length],
                notes);
        }
    }

    static IEnumerable<LiveGraphNode> Walk(LiveGraphNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var nested in Walk(child))
                yield return nested;
        }
    }

    sealed record RollLane(string Label, string Color, IReadOnlyList<RollNote> Notes);
    sealed record RollNote(double StartBeat, double DurationBeats);
}

/// <summary>Shows how the editor source maps to interpreted structure.</summary>
public sealed class LiveCodeInterpretationVisualizer : ILiveVisualizer
{
    readonly TextBlock _body = new()
    {
        FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace"),
        FontSize = 13,
        Foreground = new SolidColorBrush(Color.Parse("#E2E8F0")),
        TextWrapping = TextWrapping.Wrap,
    };

    public string Title => "Code interpretation";
    public Control View => new ScrollViewer { Content = _body };

    public void Bind(LiveVisualizerModel model)
    {
        if (model.Graph is null)
        {
            _body.Text = "Compile to interpret the buffer into tracks / patterns.";
            return;
        }

        var lines = new List<string>
        {
            $"Preset: {model.ActivePreset ?? "Live buffer"}",
            $"Transport: beat {model.Beat:0.###} · bar {model.Bar} · phrase {model.Phrase} @ {model.Bpm:0} BPM",
            "",
            "Interpreted graph:",
        };
        AppendNode(lines, model.Graph, indent: 0);
        if (!string.IsNullOrWhiteSpace(model.SourceExcerpt))
        {
            lines.Add("");
            lines.Add("Source excerpt:");
            lines.Add(model.SourceExcerpt.Trim());
        }

        _body.Text = string.Join(Environment.NewLine, lines);
    }

    static void AppendNode(List<string> lines, LiveGraphNode node, int indent)
    {
        lines.Add($"{new string(' ', indent * 2)}- {node.Label}");
        foreach (var child in node.Children)
            AppendNode(lines, child, indent + 1);
    }
}
