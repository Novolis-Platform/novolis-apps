using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

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
        new("Pen", "P", "Freehand stroke; meetup snap is off while dragging"),
        new("Line", "L", "Click vertices; Enter finishes; Ctrl+Enter closes"),
        new("Spline", "S", "Click control points; Enter / Ctrl+Enter like Line"),
        new("Box", "R", "Drag an axis-aligned rectangle"),
        new("Circle", "C", "Drag an ellipse; Shift constrains to a circle"),
        new("Speech bubble", "B", "Drag a rounded bubble with a tail"),
        new("Text", "T", "Click to place a text label"),
        new("Text box", "X", "Drag a bordered text box"),
        new("Eraser", "E", "Click or drag over strokes to erase"),
        new("Paint bucket", "K", "Click a shape to apply the current fill color (#AARRGGBB ok)"),
        new("Select", "V", "Move, resize, rotate; Shift multi-select / marquee"),
    ];

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
