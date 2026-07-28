using System.Diagnostics;
using System.Text;

namespace BooksWriterStudio.Services;

/// <summary>Writes unhandled exception logs and opens them in the default text editor.</summary>
internal static class CrashLog
{
    static int _opened;

    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                WriteAndOpen(ex, "AppDomain.UnhandledException");
            else
                WriteAndOpen(new Exception(e.ExceptionObject?.ToString() ?? "Unknown"), "AppDomain.UnhandledException");
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            WriteAndOpen(e.Exception, "TaskScheduler.UnobservedTaskException");
            e.SetObserved();
        };
    }

    public static void InstallAvalonia(Avalonia.Threading.Dispatcher dispatcher)
    {
        dispatcher.UnhandledException += (_, e) =>
        {
            WriteAndOpen(e.Exception, "Dispatcher.UnhandledException");
            e.Handled = true;
        };
    }

    public static string WriteAndOpen(Exception exception, string source)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "BooksWriterStudio",
            "crashes");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"crash-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.log");
        var sb = new StringBuilder();
        sb.AppendLine($"Books Writer Studio crash ({source})");
        sb.AppendLine($"UTC: {DateTime.UtcNow:O}");
        sb.AppendLine($"Machine: {Environment.MachineName}");
        sb.AppendLine($"OS: {Environment.OSVersion}");
        sb.AppendLine($"Runtime: {Environment.Version}");
        sb.AppendLine($"BaseDirectory: {AppContext.BaseDirectory}");
        sb.AppendLine();
        sb.AppendLine(exception.ToString());
        if (exception is AggregateException agg)
        {
            sb.AppendLine();
            sb.AppendLine("--- Flattened ---");
            foreach (var inner in agg.Flatten().InnerExceptions)
                sb.AppendLine(inner.ToString());
        }

        File.WriteAllText(path, sb.ToString());

        // Only auto-open once per process so cascading failures do not spam editors.
        if (Interlocked.Exchange(ref _opened, 1) == 0)
            OpenInDefaultEditor(path);

        return path;
    }

    static void OpenInDefaultEditor(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = true,
                });
            }
            catch
            {
                // ignored — log file is still on disk
            }
        }
    }
}
