using Microsoft.Win32;
using Merglyph.Core;

namespace Merglyph;

/// <summary>
/// Per-user HKCU Markdown associations. Registration is explicit (local debug / user action),
/// never automatic on every launch.
/// </summary>
internal static class WindowsFileAssociations
{
    private const string ProgId = "Merglyph.Markdown";

    public static void Register()
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
            return;

        using var classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes", writable: true);
        using var progId = classes.CreateSubKey(ProgId, writable: true);
        progId.SetValue(null, "Markdown document");

        using (var icon = progId.CreateSubKey("DefaultIcon", writable: true))
            icon.SetValue(null, $"\"{executable}\",0");

        using (var command = progId.CreateSubKey(@"shell\open\command", writable: true))
            command.SetValue(null, $"\"{executable}\" \"%1\"");

        foreach (var extension in SupportedMarkdownDocuments.Extensions)
        {
            using var openWith = classes.CreateSubKey($@"{extension}\OpenWithProgids", writable: true);
            openWith.SetValue(ProgId, string.Empty, RegistryValueKind.String);
        }

        using var application = classes.CreateSubKey(@"Applications\Merglyph.exe", writable: true);
        using (var command = application.CreateSubKey(@"shell\open\command", writable: true))
            command.SetValue(null, $"\"{executable}\" \"%1\"");

        using var supportedTypes = application.CreateSubKey("SupportedTypes", writable: true);
        foreach (var extension in SupportedMarkdownDocuments.Extensions)
            supportedTypes.SetValue(extension, string.Empty, RegistryValueKind.String);
    }

    public static void Unregister()
    {
        using var classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes", writable: true);
        try { classes.DeleteSubKeyTree(ProgId, throwOnMissingSubKey: false); } catch { /* best effort */ }
        try { classes.DeleteSubKeyTree(@"Applications\Merglyph.exe", throwOnMissingSubKey: false); } catch { /* best effort */ }

        foreach (var extension in SupportedMarkdownDocuments.Extensions)
        {
            try
            {
                using var openWith = classes.OpenSubKey($@"{extension}\OpenWithProgids", writable: true);
                openWith?.DeleteValue(ProgId, throwOnMissingValue: false);
            }
            catch
            {
                // best effort cleanup
            }
        }
    }
}
