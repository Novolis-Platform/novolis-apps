using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit;
using BooksWriterStudio.Services;
using BooksWriterStudio.Ui;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Manuscript.Export.Audio;
using Novolis.Avalonia.Controls;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Avalonia.Layout;
using Novolis.Avalonia.Markdown;
using Novolis.Avalonia.Manuscript;
using Novolis.Avalonia.Studio;
using Novolis.IO.Git;
using Novolis.IO.Recovery;
using Novolis.Manuscript;
using Novolis.Manuscript.Export.Pdf;

namespace BooksWriterStudio;

internal static class EditorSelectionHelper
{
    static readonly FieldInfo? EditorField =
        typeof(MarkdownSourceEditor).GetField("_editor", BindingFlags.Instance | BindingFlags.NonPublic);

    public static string? GetSelectedText(MarkdownSourceEditor editor)
    {
        if (EditorField?.GetValue(editor) is not TextEditor textEditor)
            return null;

        return textEditor.SelectionLength > 0 ? textEditor.SelectedText : null;
    }
}
