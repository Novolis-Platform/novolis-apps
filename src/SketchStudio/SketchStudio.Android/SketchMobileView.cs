using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.Controls.Sketch;
using Novolis.Avalonia.GraphicalProfile;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaOrientation = Avalonia.Layout.Orientation;

namespace SketchStudio.Android;

/// <summary>
/// Compact touch-first Sketch Studio surface for Android. The desktop head
/// retains the complete authoring toolbar; this head keeps the drawing canvas
/// and the high-frequency actions usable on a tablet.
/// </summary>
internal sealed class SketchMobileView : UserControl
{
    readonly SketchControl _sketch;
    readonly TextBlock _status;

    public SketchMobileView()
    {
        Background = GraphicalProfile.BackgroundBrush;

        _status = new TextBlock
        {
            Text = "Ready · Pen · 0 strokes",
            Foreground = GraphicalProfile.MutedBrush,
            FontSize = 12,
            Margin = new Thickness(12, 6, 12, 8),
            TextWrapping = TextWrapping.Wrap,
        };

        _sketch = new SketchControl
        {
            Tool = SketchTool.Pen,
            GridSize = 20,
            GridVisible = true,
            SnapEnabled = true,
            MeetupEnabled = true,
            StrokeColor = "#1e1e1e",
            FillColor = "#1e1e1e",
            StrokeWidth = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Document = NewDocument(),
        };
        _sketch.DocumentChanged += RefreshStatus;
        _sketch.SelectionChanged += RefreshStatus;

        var title = new TextBlock
        {
            Text = "SKETCH STUDIO / MOBILE",
            Foreground = GraphicalProfile.AccentBrush,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 1.2,
            Margin = new Thickness(12, 12, 12, 4),
        };

        var subtitle = new TextBlock
        {
            Text = "Draw freely · pinch or drag to explore the canvas",
            Foreground = GraphicalProfile.MutedBrush,
            FontSize = 12,
            Margin = new Thickness(12, 0, 12, 8),
        };

        var toolbar = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new StackPanel
            {
                Orientation = AvaloniaOrientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(12, 0, 12, 8),
                Children =
                {
                    ActionButton("Pen", () => SelectTool(SketchTool.Pen), selected: true),
                    ActionButton("Line", () => SelectTool(SketchTool.Line)),
                    ActionButton("Box", () => SelectTool(SketchTool.Rect)),
                    ActionButton("Circle", () => SelectTool(SketchTool.Ellipse)),
                    ActionButton("Select", () => SelectTool(SketchTool.Select)),
                    ActionButton("Undo", () => _sketch.Undo()),
                    ActionButton("Redo", () => _sketch.Redo()),
                    ActionButton("Clear", () =>
                    {
                        _sketch.Clear();
                        SetStatus("Canvas cleared");
                    }),
                },
            },
        };

        var header = new StackPanel
        {
            Children = { title, subtitle, toolbar },
        };

        var statusBar = new Border
        {
            Background = GraphicalProfile.SurfaceBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = _status,
        };

        var root = new DockPanel
        {
            LastChildFill = true,
            Children = { header, statusBar, _sketch },
        };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(statusBar, Dock.Bottom);
        Content = root;
    }

    static SketchDocument NewDocument() =>
        new()
        {
            Grid =
            {
                Size = 20,
                Visible = true,
                SnapEnabled = true,
            },
        };

    AvaloniaButton ActionButton(string label, Action action, bool selected = false)
    {
        var button = new AvaloniaButton
        {
            Content = label,
            MinWidth = 72,
            MinHeight = 48,
            Padding = new Thickness(12, 8),
            Background = selected
                ? GraphicalProfile.AccentFillBrush
                : GraphicalProfile.RaisedBrush,
            Foreground = selected
                ? GraphicalProfile.OnAccentFillBrush
                : GraphicalProfile.TextBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(1),
        };
        button.Click += (_, _) => action();
        return button;
    }

    void SelectTool(SketchTool tool)
    {
        _sketch.Tool = tool;
        SetStatus($"{tool} selected");
    }

    void RefreshStatus()
    {
        var count = _sketch.Document?.Elements.Count ?? 0;
        SetStatus($"{_sketch.Tool} · {count} stroke{(count == 1 ? string.Empty : "s")}");
    }

    void SetStatus(string text) => _status.Text = text;
}
