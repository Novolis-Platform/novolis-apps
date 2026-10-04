using Novolis.Avalonia.Cad.Ui;

namespace CadStudio;

/// <summary>
/// Raylib allows one GLFW host per process. Hidden CAD / ship editors must release it
/// so a second <see cref="Novolis.Avalonia.Raylib.RaylibHostControl"/> does not wait
/// on the global lock and crash the UI thread pool.
/// </summary>
internal static class CadStudioRaylibHosts
{
    public static void ReleaseHidden(
        CadEditorSurface cad,
        CadEditorSurface ship,
        bool cadVisible,
        bool shipVisible)
    {
        ArgumentNullException.ThrowIfNull(cad);
        ArgumentNullException.ThrowIfNull(ship);

        if (!cadVisible)
            cad.ModelHost.SetHostActive(false);
        if (!shipVisible)
            ship.ModelHost.SetHostActive(false);
    }
}
