using Novolis.Avalonia.Cad.Commands;
using Novolis.Avalonia.Cad.Core;
using Novolis.Avalonia.Cad.Services;

namespace DraftStudio.Unit;

internal static class DraftTestHarness
{
    public static (CadEditorSettings Settings, CadDocumentSession Session, CadCommandBus Bus, CadCommandDispatcher Dispatcher) Create(
        string? root = null)
    {
        root ??= Path.Combine(Path.GetTempPath(), "draft-unit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var settings = new CadEditorSettings(root);
        var session = new CadDocumentSession(settings);
        var bus = new CadCommandBus(session);
        var dispatcher = new CadCommandDispatcher(session, bus, settings);
        session.OpenOrCreateDefault();
        return (settings, session, bus, dispatcher);
    }

    public static void DispatchOk(CadCommandDispatcher dispatcher, string prompt)
    {
        var err = dispatcher.TryDispatch(prompt);
        if (err is not null)
            throw new InvalidOperationException($"Dispatch failed for '{prompt}': {err}");
    }
}
