using Microsoft.Win32;

namespace Novolis.Ndjson.App;

internal static class WindowsFileAssociations
{
    private const string ProgId = "Novolis.NdjsonViewer";

    public static void Register()
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
            return;

        using var classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes", writable: true);
        using var progId = classes.CreateSubKey(ProgId, writable: true);
        progId.SetValue(null, "Novolis NDJSON document");

        using (var command = progId.CreateSubKey(@"shell\open\command", writable: true))
            command.SetValue(null, $"\"{executable}\" \"%1\"");

        using var openWith = classes.CreateSubKey(@".ndjson\OpenWithProgids", writable: true);
        openWith.SetValue(ProgId, string.Empty, RegistryValueKind.String);

        using var application = classes.CreateSubKey(@"Applications\Novolis.Ndjson.App.exe", writable: true);
        using (var applicationCommand = application.CreateSubKey(@"shell\open\command", writable: true))
            applicationCommand.SetValue(null, $"\"{executable}\" \"%1\"");

        using var supportedTypes = application.CreateSubKey("SupportedTypes", writable: true);
        supportedTypes.SetValue(".ndjson", string.Empty);
    }
}
