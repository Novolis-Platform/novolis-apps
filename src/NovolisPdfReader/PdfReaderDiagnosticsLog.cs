using System.Globalization;
using System.Text;
using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Platform;

namespace NovolisPdfReader;

/// <summary>Append-only local diagnostics log for PDF open failures.</summary>
public sealed class PdfReaderDiagnosticsLog
{
    private readonly Lock _gate = new();

    /// <summary>Process-wide log used before MAUI dependency injection is ready.</summary>
    public static PdfReaderDiagnosticsLog Shared { get; } = new();

    /// <summary>Creates the log under the current user's Novolis data folder.</summary>
    public PdfReaderDiagnosticsLog()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "pdf-reader");
        Directory.CreateDirectory(directory);
        FilePath = Path.Combine(directory, "diagnostics.log");
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

        var block = new StringBuilder();
        block.AppendLine("========");
        block.Append("utc: ").AppendLine(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        block.Append("stage: ").AppendLine(stage);
        block.Append("message: ").AppendLine(message);
        block.Append("pid: ").AppendLine(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        AppendRequest(block, request);
        if (exception is not null)
            AppendException(block, exception);
        if (diagnostics is { Count: > 0 })
        {
            block.AppendLine("engineDiagnostics:");
            foreach (var entry in diagnostics)
                AppendDiagnostic(block, entry, "  ");
        }

        block.AppendLine();
        var text = block.ToString();
        lock (_gate)
        {
            try
            {
                File.AppendAllText(FilePath, text, Encoding.UTF8);
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
        lock (_gate)
        {
            if (!File.Exists(FilePath))
                return string.Empty;
            var text = File.ReadAllText(FilePath, Encoding.UTF8);
            return text.Length <= maximumCharacters
                ? text
                : text[^maximumCharacters..];
        }
    }

    private static void AppendRequest(StringBuilder block, PdfOpenRequest? request)
    {
        if (request is null)
            return;

        var descriptor = request.Descriptor;
        block.Append("displayName: ").AppendLine(descriptor.DisplayName);
        block.Append("stableId: ").AppendLine(descriptor.StableId ?? string.Empty);
        block.Append("declaredBytes: ").AppendLine(
            descriptor.Length?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        var path = descriptor.StableId;
        if (string.IsNullOrWhiteSpace(path) || !LooksLikePath(path))
            return;

        try
        {
            var exists = File.Exists(path);
            block.Append("pathExists: ").AppendLine(exists.ToString());
            if (!exists)
                return;

            var info = new FileInfo(path);
            block.Append("fileBytes: ").AppendLine(info.Length.ToString(CultureInfo.InvariantCulture));
            block.Append("fileUtc: ").AppendLine(info.LastWriteTimeUtc.ToString("O", CultureInfo.InvariantCulture));
            block.Append("headerHex: ").AppendLine(ReadHeaderHex(path));
        }
        catch (Exception probeException)
        {
            block.Append("pathProbe: ").AppendLine(probeException.ToString());
        }
    }

    private static void AppendException(StringBuilder block, Exception exception)
    {
        block.Append("exceptionType: ").AppendLine(exception.GetType().FullName);
        block.AppendLine("exception:");
        block.AppendLine(exception.ToString());
        if (exception is PdfReaderException reader)
            AppendDiagnostic(block, reader.Diagnostic, string.Empty);

        var inner = exception.InnerException;
        var depth = 1;
        while (inner is not null && depth <= 8)
        {
            block.Append("inner[").Append(depth.ToString(CultureInfo.InvariantCulture)).Append("]: ")
                .AppendLine(inner.GetType().FullName);
            block.AppendLine(inner.ToString());
            if (inner is PdfReaderException innerReader)
                AppendDiagnostic(block, innerReader.Diagnostic, "  ");
            inner = inner.InnerException;
            depth++;
        }
    }

    private static void AppendDiagnostic(
        StringBuilder block,
        PdfDiagnosticEntry entry,
        string indent)
    {
        block.Append(indent).Append("diagnostic.severity: ").AppendLine(entry.Severity.ToString());
        block.Append(indent).Append("diagnostic.code: ").AppendLine(entry.Code);
        block.Append(indent).Append("diagnostic.message: ").AppendLine(entry.Message);
        if (entry.ByteOffset is { } offset)
            block.Append(indent).Append("diagnostic.byteOffset: ")
                .AppendLine(offset.ToString(CultureInfo.InvariantCulture));
        if (entry.ObjectNumber is { } objectNumber)
            block.Append(indent).Append("diagnostic.objectNumber: ")
                .AppendLine(objectNumber.ToString(CultureInfo.InvariantCulture));
        if (entry.PageIndex is { } pageIndex)
            block.Append(indent).Append("diagnostic.pageIndex: ")
                .AppendLine(pageIndex.ToString(CultureInfo.InvariantCulture));
        if (entry.Exception is { } nested)
            block.Append(indent).Append("diagnostic.exception: ").AppendLine(nested.ToString());
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
