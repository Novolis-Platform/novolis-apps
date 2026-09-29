using System.Text.Json;
using Merglyph.Core;

namespace Merglyph;

public sealed record RecentDocument(
    string Name,
    string Content,
    DateTimeOffset LastOpenedUtc);

public sealed class RecentDocumentStore
{
    public const int MaximumRecentDocuments = 10;

    private const string IndexFileName = "recent-documents.json";
    private const string ContentDirectoryName = "recent-documents";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);

    public async ValueTask<IReadOnlyList<RecentDocument>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var entries = await ReadEntriesAsync(cancellationToken);
            return await ReadDocumentsAsync(entries, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<IReadOnlyList<RecentDocument>> RememberAsync(
        MarkdownDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var entries = await ReadEntriesAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var existing = entries.FirstOrDefault(
                entry => string.Equals(entry.Name, document.Name.Value, StringComparison.OrdinalIgnoreCase));
            var contentFileName = existing?.ContentFileName ?? $"{Guid.NewGuid():N}.md";

            Directory.CreateDirectory(ContentDirectoryPath);
            await File.WriteAllTextAsync(
                Path.Combine(ContentDirectoryPath, contentFileName),
                document.Content.Value,
                cancellationToken);

            var updated = new RecentDocumentEntry(
                document.Name.Value,
                contentFileName,
                now);

            entries.RemoveAll(entry =>
                string.Equals(entry.Name, document.Name.Value, StringComparison.OrdinalIgnoreCase));
            entries.Add(updated);
            entries.Sort(static (left, right) => right.LastOpenedUtc.CompareTo(left.LastOpenedUtc));

            var removed = entries.Skip(MaximumRecentDocuments).ToArray();
            entries = entries.Take(MaximumRecentDocuments).ToList();
            await WriteEntriesAsync(entries, cancellationToken);

            foreach (var removedEntry in removed)
            {
                TryDeleteContent(removedEntry.ContentFileName);
            }

            return await ReadDocumentsAsync(entries, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private string IndexPath => Path.Combine(FileSystem.AppDataDirectory, IndexFileName);

    private string ContentDirectoryPath =>
        Path.Combine(FileSystem.AppDataDirectory, ContentDirectoryName);

    private async Task<List<RecentDocumentEntry>> ReadEntriesAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(IndexPath))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(IndexPath, cancellationToken);
            return JsonSerializer.Deserialize<List<RecentDocumentEntry>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    private async Task WriteEntriesAsync(
        IReadOnlyList<RecentDocumentEntry> entries,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(FileSystem.AppDataDirectory);
        var temporaryPath = $"{IndexPath}.tmp";
        var json = JsonSerializer.Serialize(entries, JsonOptions);
        await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
        File.Move(temporaryPath, IndexPath, overwrite: true);
    }

    private async Task<IReadOnlyList<RecentDocument>> ReadDocumentsAsync(
        IEnumerable<RecentDocumentEntry> entries,
        CancellationToken cancellationToken)
    {
        var documents = new List<RecentDocument>(MaximumRecentDocuments);
        foreach (var entry in entries
                     .OrderByDescending(entry => entry.LastOpenedUtc)
                     .Take(MaximumRecentDocuments))
        {
            if (!IsSafeContentFileName(entry.ContentFileName))
                continue;

            try
            {
                var content = await File.ReadAllTextAsync(
                    Path.Combine(ContentDirectoryPath, entry.ContentFileName),
                    cancellationToken);
                documents.Add(new RecentDocument(entry.Name, content, entry.LastOpenedUtc));
            }
            catch (FileNotFoundException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (IOException)
            {
            }
        }

        return documents;
    }

    private void TryDeleteContent(string contentFileName)
    {
        if (!IsSafeContentFileName(contentFileName))
            return;

        try
        {
            File.Delete(Path.Combine(ContentDirectoryPath, contentFileName));
        }
        catch (IOException)
        {
        }
    }

    private static bool IsSafeContentFileName(string contentFileName) =>
        !string.IsNullOrWhiteSpace(contentFileName)
        && string.Equals(Path.GetFileName(contentFileName), contentFileName, StringComparison.Ordinal)
        && contentFileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

    private sealed record RecentDocumentEntry(
        string Name,
        string ContentFileName,
        DateTimeOffset LastOpenedUtc);
}
