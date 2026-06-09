using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;

namespace LiveStudio.Components;

internal sealed class LiveCodeEditor : Border
{
    private static readonly IBrush EditorBackground = new SolidColorBrush(Color.Parse("#0B1020"));
    private static readonly IBrush EditorForeground = new SolidColorBrush(Color.Parse("#E2E8F0"));
    private static readonly IBrush GutterBackground = new SolidColorBrush(Color.Parse("#111827"));
    private static readonly IBrush CurrentLineBrush = new SolidColorBrush(Color.Parse("#172033"));
    private static readonly IBrush BorderBrushColor = new SolidColorBrush(Color.Parse("#243047"));

    private readonly TextEditor _editor;

    public LiveCodeEditor()
    {
        Background = EditorBackground;
        BorderBrush = BorderBrushColor;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(12);
        Padding = new Thickness(0);

        _editor = new TextEditor
        {
            FontFamily = new FontFamily("Cascadia Mono,Consolas,Menlo,monospace"),
            FontSize = 14,
            Background = EditorBackground,
            Foreground = EditorForeground,
            ShowLineNumbers = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(12, 10),
        };

        _editor.Options.EnableHyperlinks = false;
        _editor.Options.EnableEmailHyperlinks = false;
        _editor.Options.HighlightCurrentLine = true;
        _editor.Options.ConvertTabsToSpaces = true;
        _editor.Options.IndentationSize = 4;
        _editor.TextArea.TextView.CurrentLineBackground = CurrentLineBrush;
        _editor.TextArea.TextView.LinkTextForegroundBrush = new SolidColorBrush(Color.Parse("#7DD3FC"));
        _editor.TextArea.SelectionBrush = new SolidColorBrush(Color.Parse("#1D4ED866"));
        _editor.TextArea.SelectionForeground = EditorForeground;

        var highlighting = HighlightingManager.Instance.GetDefinition("C#");
        if (highlighting is not null)
            _editor.SyntaxHighlighting = highlighting;

        _editor.KeyDown += OnKeyDown;
        Child = _editor;
    }

    public event EventHandler? CompileRequested;

    public string Text
    {
        get => _editor.Text;
        set => _editor.Text = value;
    }

    public void FocusEditor() => _editor.Focus();

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            CompileRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }
}
