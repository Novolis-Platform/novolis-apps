using System.Globalization;
using System.Text;
using Novolis.Manuscript;
using Novolis.Manuscript.Editorial;
using Novolis.Manuscript.Metrics;

namespace BooksWriterStudio.Services;

/// <summary>Read-only continuity reports over Manuscript doctor / metrics / ascii / editorial libraries.</summary>
internal static class ContinuityDiagnostics
{
    public static IReadOnlyList<DiagnosticFinding> RunDoctorAndDebt(BookInfo book)
    {
        ArgumentNullException.ThrowIfNull(book);
        var findings = new List<DiagnosticFinding>(ManuscriptDoctor.Diagnose(book));
        try
        {
            findings.AddRange(ManuscriptMetadataDebt.Diagnose(ManuscriptPaths.ResolveChaptersDirectory(book)));
        }
        catch (Exception ex)
        {
            findings.Add(new DiagnosticFinding(DiagnosticSeverity.Warning, "metadata-debt-failed", ex.Message));
        }

        return findings;
    }

    public static BookMetricsDto ComputeMetrics(string workspaceRoot, BookInfo book)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(book);
        var seriesId = string.IsNullOrWhiteSpace(book.SeriesId) ? "books" : book.SeriesId;
        return ManuscriptMetrics.ComputeOne(workspaceRoot, seriesId, book.Id);
    }

    public static string FormatMetrics(BookMetricsDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var sb = new StringBuilder();
        sb.AppendLine(
            $"{dto.Series}/{dto.Book}" + (string.IsNullOrWhiteSpace(dto.Title) ? "" : $" — {dto.Title}"));
        sb.AppendLine(
            $"Words: {dto.TotalWords.ToString("N0", CultureInfo.InvariantCulture)}  ·  TODOs: {dto.TotalTodos}  ·  ~{dto.EstimatedHours.ToString("0.0", CultureInfo.InvariantCulture)}h");
        if (dto.TargetWords is { } target)
            sb.AppendLine($"Target: {target.ToString("N0", CultureInfo.InvariantCulture)} words");
        sb.AppendLine();
        foreach (var ch in dto.Chapters.Take(40))
            sb.AppendLine($"  {ch.File}: {ch.Words}w  todos={ch.Todos}");
        if (dto.Chapters.Count > 40)
            sb.AppendLine($"  … {(dto.Chapters.Count - 40).ToString(CultureInfo.InvariantCulture)} more chapters");
        return sb.ToString().TrimEnd();
    }

    public static IReadOnlyList<DiagnosticFinding> ScanAscii(BookInfo book)
    {
        ArgumentNullException.ThrowIfNull(book);
        var dir = ManuscriptPaths.ResolveChaptersDirectory(book);
        return ManuscriptAscii.ScanChaptersDirectory(dir, limit: 200)
            .Select(i => new DiagnosticFinding(
                DiagnosticSeverity.Warning,
                "ascii-nonascii",
                $"U+{i.Codepoint:X4} at {i.Line}:{i.Column}",
                i.Path))
            .ToList();
    }

    public static string CharacterSlicesMarkdown(BookInfo book, string? filter = null)
    {
        ArgumentNullException.ThrowIfNull(book);
        return ManuscriptCharacterSlices.Build(book).ToMarkdown(filter);
    }

    public static IReadOnlyList<DiagnosticFinding> RunEditorial(BookInfo book)
    {
        ArgumentNullException.ThrowIfNull(book);
        var dir = ManuscriptPaths.ResolveChaptersDirectory(book);
        return EditorialAnalyzer.AnalyzeChaptersDir(dir, EditorialProfiles.Calypso());
    }
}
