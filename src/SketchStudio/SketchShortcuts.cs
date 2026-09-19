using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.Controls.Sketch;

namespace SketchStudio;

/// <summary>Single source of truth for toolbar tips, F1 help, and README shortcut tables.</summary>
internal static class SketchShortcuts
{
    public const int TipShowDelayMs = 200;

    public static readonly ShortcutRow[] File =
    [
        new("New", "Ctrl+N", "Start a blank sketch (prompts if dirty)"),
        new("Open", "Ctrl+O", "Open a .sketchjson file"),
        new("Recent", "—", "Reopen from the last 8 saved/opened paths"),
        new("Save", "Ctrl+S", "Save to the current path (or Save As)"),
        new("Save As", "Ctrl+Shift+S", "Save as a new .sketchjson path"),
    ];

    public static readonly ShortcutRow[] Tools =
    [
        new("Pen", "Ctrl+P", "Freehand stroke; meetup snap is off while dragging"),
        new("Line", "Ctrl+L", "Click vertices; Enter finishes; Ctrl+Enter closes"),
        new("Spline", "Ctrl+U", "Click control points; Enter / Ctrl+Enter like Line"),
        new("Box", "Ctrl+R", "Drag an axis-aligned rectangle"),
        new("Circle", "Ctrl+C", "Drag an ellipse; Shift constrains to a circle"),
        new("Speech bubble", "Ctrl+B", "Drag a rounded bubble with a tail"),
        new("Text", "Ctrl+T", "Click to place a text label"),
        new("Text box", "Ctrl+X", "Drag a bordered text box"),
        new("Eraser", "Ctrl+E", "Click or drag over strokes to erase"),
        new("Paint bucket", "Ctrl+K", "Click a shape to apply the current fill color (#AARRGGBB ok)"),
        new("Select", "Ctrl+M", "Move, resize, rotate; Shift multi-select / marquee"),
    ];

    /// <summary>
    /// Tool switching is hold-Ctrl then press the letter so typing never selects a tool.
    /// Ctrl+S stays Save (Spline is U). Ctrl+V stays paste image (Select is M).
    /// Shift / Alt / Meta with Ctrl does not switch tools.
    /// </summary>
    public static SketchTool? MatchTool(Key key, KeyModifiers modifiers)
    {
        if (!modifiers.HasFlag(KeyModifiers.Control))
            return null;
        if ((modifiers & (KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta)) != 0)
            return null;

        return key switch
        {
            Key.P => SketchTool.Pen,
            Key.L => SketchTool.Line,
            Key.U => SketchTool.Spline,
            Key.R => SketchTool.Rect,
            Key.C => SketchTool.Ellipse,
            Key.B => SketchTool.SpeechBubble,
            Key.T => SketchTool.Text,
            Key.X => SketchTool.TextBox,
            Key.E => SketchTool.Eraser,
            Key.K => SketchTool.Fill,
            Key.M => SketchTool.Select,
            _ => null
        };
    }

    public static string? ShortcutForTool(SketchTool tool) => tool switch
    {
        SketchTool.Pen => "Ctrl+P",
        SketchTool.Line => "Ctrl+L",
        SketchTool.Spline => "Ctrl+U",
        SketchTool.Rect => "Ctrl+R",
        SketchTool.Ellipse => "Ctrl+C",
        SketchTool.SpeechBubble => "Ctrl+B",
        SketchTool.Text => "Ctrl+T",
        SketchTool.TextBox => "Ctrl+X",
        SketchTool.Eraser => "Ctrl+E",
        SketchTool.Fill => "Ctrl+K",
        SketchTool.Select => "Ctrl+M",
        _ => null
    };

    /// <summary>Letter shown on the bound control while Control is held. Null if the shortcut is not a Ctrl chord.</summary>
    public static string? HoldCtrlLetter(string? shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut) || shortcut == "—")
            return null;

        var parts = shortcut.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return null;

        var hasCtrl = false;
        var hasShift = false;
        string? key = null;
        foreach (var part in parts)
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)
                || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                hasCtrl = true;
                continue;
            }

            if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                hasShift = true;
                continue;
            }

            if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)
                || part.Equals("Meta", StringComparison.OrdinalIgnoreCase)
                || part.Equals("Win", StringComparison.OrdinalIgnoreCase))
                continue;

            key = part;
        }

        if (!hasCtrl || key is null)
            return null;

        var letter = key.Equals("Enter", StringComparison.OrdinalIgnoreCase) ? "↵"
            : key.Length == 1 ? key.ToUpperInvariant()
            : null;
        if (letter is null)
            return null;

        return hasShift ? "⇧" + letter : letter;
    }

    public static readonly ShortcutRow[] Edit =
    [
        new("Undo", "Ctrl+Z", "Undo last document change"),
        new("Redo", "Ctrl+Y", "Redo"),
        new("Delete selection", "Del / Backspace", "Remove selected elements"),
        new("Select all", "Ctrl+A", "Select every element (Select tool)"),
        new("Fuse", "Ctrl+G", "Group ≥2 selected shapes to move/transform together"),
        new("Ungroup", "Ctrl+Shift+G", "Clear groupId on the selection"),
        new("Complete line/spline", "Enter", "Commit open polyline / spline"),
        new("Close shape", "Ctrl+Enter", "Close with ≥3 points (or click the start vertex)"),
        new("Cancel drawing", "Esc", "Discard in-progress line/spline/drag"),
        new("Paste image", "Ctrl+V", "Insert clipboard bitmap at viewport center"),
        new("Pan canvas", "Space + drag", "Temporary pan while Space is held"),
    ];

    public static readonly ShortcutRow[] Export =
    [
        new("Copy PNG", "—", "Clipboard bitmap with opaque white background"),
        new("Save PNG file", "—", "Write a .png file (opaque) to disk"),
        new("Copy SVG", "—", "Clipboard SVG text"),
        new("Save SVG file", "—", "Write a .svg file to disk"),
    ];

    public static readonly ShortcutRow[] Help =
    [
        new("Keyboard shortcuts", "F1", "Show this shortcut reference"),
    ];

    public static void ApplyTip(Control control, string tip)
    {
        ToolTip.SetTip(control, tip);
        ToolTip.SetShowDelay(control, TipShowDelayMs);
    }

    public static string FormatTip(string title, string? shortcut, string detail) =>
        string.IsNullOrWhiteSpace(shortcut) || shortcut == "—"
            ? $"{title}\n{detail}"
            : $"{title}  ({shortcut})\n{detail}";

    public static async Task ShowHelpAsync(Window owner)
    {
        var body = new StackPanel { Spacing = 12 };
        void Section(string heading, ShortcutRow[] rows)
        {
            body.Children.Add(new TextBlock
            {
                Text = heading,
                FontWeight = FontWeight.SemiBold,
                FontSize = 14
            });
            foreach (var row in rows)
            {
                body.Children.Add(new TextBlock
                {
                    Text = $"{row.Action}  ·  {row.Keys}",
                    FontWeight = FontWeight.Medium,
                    FontSize = 12
                });
                body.Children.Add(new TextBlock
                {
                    Text = row.Detail,
                    Opacity = 0.8,
                    FontSize = 12,
                    Margin = new Thickness(0, 0, 0, 6),
                    TextWrapping = TextWrapping.Wrap
                });
            }
        }

        Section("File", File);
        Section("Tools", Tools);
        Section("Edit & canvas", Edit);
        Section("Export", Export);
        Section("Help", Help);

        var dialog = new Window
        {
            Title = "Sketch Studio — keyboard shortcuts",
            Width = 520,
            Height = 640,
            MinWidth = 400,
            MinHeight = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new DockPanel
            {
                Margin = new Thickness(16),
                Children =
                {
                    new ScrollViewer
                    {
                        Content = body,
                        VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
                    }
                }
            }
        };

        dialog.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Escape or Key.F1)
            {
                dialog.Close();
                e.Handled = true;
            }
        };

        await dialog.ShowDialog(owner);
    }

    public readonly record struct ShortcutRow(string Action, string Keys, string Detail);
}
