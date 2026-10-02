using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Platform;
using Novolis.Storage.Ndjson;
#if ANDROID
using Novolis.IO.Platform.Android;
#endif

namespace NovolisPdfReader;

/// <summary>Append-only local diagnostics log for PDF open failures.</summary>
public sealed class PdfReaderDiagnosticsLog
{
    private readonly Lock _gate = new();
    private readonly NdjsonStore _store;

    /// <summary>Process-wide log used before MAUI dependency injection is ready.</summary>
    public static PdfReaderDiagnosticsLog Shared { get; } = new();

    /// <summary>Creates the log under the current user's Novolis data folder.</summary>
    public PdfReaderDiagnosticsLog()
    {
#if ANDROID
        var directory = AndroidAppStorage.DefaultRoot("NovolisPdfReader");
#else
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "pdf-reader");
#endif
        Directory.CreateDirectory(directory);
        FilePath = Path.Combine(directory, "diagnostics.ndjson");
        _store = new NdjsonStore(FilePath);
    }

    /// <summary>Absolute path of the append-only log file.</summary>
    public string FilePath { get; }

    /// <summary>Hooks process-wide crash logging once.</summary>
    public void InstallProcessHooks()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Write(
                "unhandled",
                args.IsTerminating ? "AppDomain terminating exception." : "AppDomain exception.",
                exception: args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
            Write("unobserved-task", "Unobserved task exception.", exception: args.Exception);
    }

    /// <summary>Writes a structured diagnostics block.</summary>
    public void Write(
        string stage,
        string message,
        PdfOpenRequest? request = null,
        Exception? exception = null,
        IReadOnlyList<PdfDiagnosticEntry>? diagnostics = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stage);
        ArgumentNullException.ThrowIfNull(message);

        var record = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["utc"] = DateTimeOffset.UtcNow,
            ["stage"] = stage,
            ["message"] = message,
            ["pid"] = Environment.ProcessId,
            ["request"] = CreateRequestRecord(request),
            ["exception"] = exception is null ? null : CreateExceptionRecord(exception),
            ["engineDiagnostics"] = diagnostics is { Count: > 0 }
                ? diagnostics.Select(CreateDiagnosticRecord).ToArray()
                : null,
        };
        lock (_gate)
        {
            try
            {
                _store.Append(record, flushToDisk: exception is not null);
            }
            catch (Exception writeException)
            {
                System.Diagnostics.Debug.WriteLine(writeException);
            }
        }
    }

    /// <summary>Returns the trailing portion of the log for in-app display.</summary>
    public string ReadTail(int maximumCharacters = 12_000)
    {
        if (maximumCharacters <= 0)
            return string.Empty;

        lock (_gate)
        {
            if (!File.Exists(FilePath))
                return string.Empty;

            using var stream = new FileStream(
                FilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            var byteBudget = Math.Max(1024L, maximumCharacters * 4L);
            stream.Seek(Math.Max(0, stream.Length - byteBudget), SeekOrigin.Begin);
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();
            return text.Length <= maximumCharacters
                ? text
                : text[^maximumCharacters..];
        }
    }

    private static object? CreateRequestRecord(PdfOpenRequest? request)
    {
        if (request is null)
            return null;

        var descriptor = request.Descriptor;
        var record = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["displayName"] = descriptor.DisplayName,
            ["stableId"] = descriptor.StableId,
            ["declaredBytes"] = descriptor.Length,
        };
        var path = descriptor.StableId;
        if (string.IsNullOrWhiteSpace(path) || !LooksLikePath(path))
            return record;

        try
        {
            var exists = File.Exists(path);
            record["pathExists"] = exists;
            if (!exists)
                return record;

            var info = new FileInfo(path);
            record["fileBytes"] = info.Length;
            record["fileUtc"] = info.LastWriteTimeUtc;
            record["headerHex"] = ReadHeaderHex(path);
        }
        catch (Exception probeException)
        {
            record["pathProbeException"] = probeException.ToString();
        }

        return record;
    }

    private static object CreateExceptionRecord(Exception exception)
    {
        var record = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = exception.GetType().FullName,
            ["text"] = exception.ToString(),
        };
        var diagnostics = new List<object>();
        if (exception is PdfReaderException reader)
            diagnostics.Add(CreateDiagnosticRecord(reader.Diagnostic));

        var inner = exception.InnerException;
        for (var depth = 1; inner is not null && depth <= 8; depth++)
        {
            var innerRecord = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["type"] = inner.GetType().FullName,
                ["text"] = inner.ToString(),
            };
            if (inner is PdfReaderException innerReader)
                innerRecord["diagnostic"] = CreateDiagnosticRecord(innerReader.Diagnostic);
            diagnostics.Add(innerRecord);
            inner = inner.InnerException;
        }

        record["inner"] = diagnostics.Count == 0 ? null : diagnostics;
        return record;
    }

    private static object CreateDiagnosticRecord(PdfDiagnosticEntry entry)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["severity"] = entry.Severity.ToString(),
            ["code"] = entry.Code,
            ["message"] = entry.Message,
            ["byteOffset"] = entry.ByteOffset,
            ["objectNumber"] = entry.ObjectNumber,
            ["pageIndex"] = entry.PageIndex,
            ["exception"] = entry.Exception?.ToString(),
        };
    }

    private static bool LooksLikePath(string value) =>
        value.Contains(Path.DirectorySeparatorChar)
        || value.Contains(Path.AltDirectorySeparatorChar)
        || (value.Length >= 3 && char.IsLetter(value[0]) && value[1] == ':');

    private static string ReadHeaderHex(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        var buffer = new byte[32];
        var read = stream.Read(buffer, 0, buffer.Length);
        return Convert.ToHexString(buffer.AsSpan(0, read));
    }
}
