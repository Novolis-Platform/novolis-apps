using Novolis.Avalonia.Cad.Commands;
using Novolis.Avalonia.Cad.Core;
using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Cad.Session;

namespace CadStudio;

/// <summary>
/// Isolated CAD session used only for Ship MODEL projection. Must never share a
/// document with the product drafting session.
/// </summary>
internal sealed class ShipCadSession
{
    public ShipCadSession(string dataRoot)
    {
        Settings = new CadEditorSettings(Path.Combine(dataRoot, "ships"), "model-projection");
        Document = new CadDocumentSession(Settings);
        Bus = new CadCommandBus(Document);
        Dispatcher = new CadCommandDispatcher(Document, Bus, Settings);
        Service = new CadSessionService(Document, Settings, Bus, Dispatcher)
        {
            AppId = "cad-studio-ship",
            AppTitle = "Novolis CAD Studio",
            ExportRoot = Path.Combine(Settings.DataRoot, "exports"),
        };
    }

    public CadEditorSettings Settings { get; }

    public CadDocumentSession Document { get; }

    public CadCommandBus Bus { get; }

    public CadCommandDispatcher Dispatcher { get; }

    public CadSessionService Service { get; }
}
