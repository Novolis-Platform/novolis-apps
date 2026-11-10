using Merglyph.Core;

namespace Merglyph;

public delegate ValueTask<Stream> OpenDocumentStream(CancellationToken cancellationToken);

/// <summary>Persists Markdown body to the open document's storage target.</summary>
public delegate ValueTask SaveDocumentContent(
    string content,
    CancellationToken cancellationToken);

public sealed record DocumentOpenRequest(
    DocumentName Name,
    OpenDocumentStream OpenReadAsync,
    string? SourceDirectory = null,
    string? DisplayPath = null,
    SaveDocumentContent? SaveAsync = null);

/// <summary>Holds the open Markdown document and optional save binder.</summary>
public sealed class DocumentSession(MarkdownDocumentReader reader)
{
    private SaveDocumentContent? _save;

    public MarkdownDocument? Current { get; private set; }

    /// <summary>True when the in-memory body differs from the last open/save.</summary>
    public bool IsDirty { get; private set; }

    /// <summary>True when a writable target is bound for the current document.</summary>
    public bool CanSave => _save is not null;

    public async ValueTask<MarkdownDocument> OpenAsync(
        DocumentOpenRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var stream = await request.OpenReadAsync(cancellationToken);
        var document = await reader.ReadAsync(request.Name, stream, cancellationToken);
        Current = document with
        {
            SourceDirectory = request.SourceDirectory,
            DisplayPath = request.DisplayPath,
        };
        _save = request.SaveAsync;
        IsDirty = false;
        return Current;
    }

    /// <summary>Replaces the in-memory Markdown body.</summary>
    public void SetContent(string content)
    {
        if (Current is null)
            throw new InvalidOperationException("No Markdown document is open.");

        content ??= string.Empty;
        if (string.Equals(Current.Content.Value, content, StringComparison.Ordinal))
            return;

        Current = Current with { Content = new MarkdownText(content) };
        IsDirty = true;
    }

    /// <summary>Writes the current body through the bound save target.</summary>
    public async ValueTask SaveAsync(CancellationToken cancellationToken = default)
    {
        if (Current is null)
            throw new InvalidOperationException("No Markdown document is open.");
        if (_save is null)
            throw new InvalidOperationException("This document cannot be saved in place.");

        await _save(Current.Content.Value, cancellationToken).ConfigureAwait(false);
        IsDirty = false;
    }

    /// <summary>Rebinds save after a Save As created a new writable target.</summary>
    public void BindSave(
        DocumentOpenRequest request,
        MarkdownDocument document)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(document);
        Current = document;
        _save = request.SaveAsync;
        IsDirty = false;
    }
}
