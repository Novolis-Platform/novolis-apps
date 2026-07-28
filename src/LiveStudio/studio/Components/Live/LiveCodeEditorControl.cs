using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Highlighting;

namespace LiveStudio.Components.Live;

/// <summary>
/// Full Live code editor (AvaloniaEdit) with Live DSL completion.
/// Designed to move into Novolis.Avalonia.Live.
/// </summary>
public sealed class LiveCodeEditorControl : Border
{
    static readonly IBrush EditorBackground = new SolidColorBrush(Color.Parse("#0B1020"));
    static readonly IBrush EditorForeground = new SolidColorBrush(Color.Parse("#E2E8F0"));
    static readonly IBrush GutterBackground = new SolidColorBrush(Color.Parse("#111827"));
    static readonly IBrush CurrentLineBrush = new SolidColorBrush(Color.Parse("#172033"));
    static readonly IBrush BorderBrushColor = new SolidColorBrush(Color.Parse("#243047"));

    readonly TextEditor _editor;
    CompletionWindow? _completion;

    public LiveCodeEditorControl()
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
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
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
        _editor.TextArea.TextView.LineTransformers.Clear();

        var highlighting = HighlightingManager.Instance.GetDefinition("C#");
        if (highlighting is not null)
            _editor.SyntaxHighlighting = highlighting;

        _editor.TextArea.TextEntering += OnTextEntering;
        _editor.TextArea.TextEntered += OnTextEntered;
        _editor.KeyDown += OnKeyDown;
        Child = _editor;

        Text = LiveDemoCatalog.DefaultBuffer;
    }

    public event EventHandler? CompileRequested;

    public string Text
    {
        get => _editor.Text;
        set
        {
            if (_editor.Text == value)
                return;
            _editor.Text = value;
        }
    }

    public void FocusEditor() => _editor.Focus();

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5
            || (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control)))
        {
            CompileRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            ShowCompletion(force: true);
            e.Handled = true;
        }
    }

    void OnTextEntering(object? sender, TextInputEventArgs e)
    {
        if (e.Text is not { Length: > 0 })
            return;

        if (_completion is not null && !char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '.')
            _completion.CompletionList.RequestInsertion(e);
    }

    void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        if (e.Text is "." or "(")
            ShowCompletion(force: true);
        else if (e.Text is { Length: 1 } && char.IsLetter(e.Text[0]))
            ShowCompletion(force: false);
    }

    void ShowCompletion(bool force)
    {
        var word = GetWordBeforeCaret();
        if (!force && word.Length < 1)
            return;

        _completion = new CompletionWindow(_editor.TextArea);
        var data = _completion.CompletionList.CompletionData;
        foreach (var item in LiveDslCompletionProvider.GetCompletions(word))
            data.Add(item);

        if (data.Count == 0)
        {
            _completion = null;
            return;
        }

        _completion.Closed += (_, _) => _completion = null;
        _completion.Show();
    }

    string GetWordBeforeCaret()
    {
        var offset = _editor.CaretOffset;
        var doc = _editor.Document;
        if (offset <= 0)
            return string.Empty;

        var start = offset;
        while (start > 0)
        {
            var ch = doc.GetCharAt(start - 1);
            if (!char.IsLetterOrDigit(ch) && ch != '.' && ch != '_')
                break;
            start--;
        }

        return doc.GetText(start, offset - start);
    }
}
