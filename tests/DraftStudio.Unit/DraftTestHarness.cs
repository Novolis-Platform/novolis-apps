using DraftStudio.Commands;
using DraftStudio.Core;
using DraftStudio.Services;

namespace DraftStudio.Unit;

internal static class DraftTestHarness
{
    public static (DraftSettingsStore Settings, DraftSession Session, DraftCommandBus Bus, DraftCommandDispatcher Dispatcher) Create(
        string? root = null)
    {
        root ??= Path.Combine(Path.GetTempPath(), "draft-unit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var settings = new DraftSettingsStore(root);
        var session = new DraftSession(settings);
        var bus = new DraftCommandBus(session);
        var dispatcher = new DraftCommandDispatcher(session, bus, settings);
        session.OpenOrCreateDefault();
        return (settings, session, bus, dispatcher);
    }

    public static void DispatchOk(DraftCommandDispatcher dispatcher, string prompt)
    {
        var err = dispatcher.TryDispatch(prompt);
        if (err is not null)
            throw new InvalidOperationException($"Dispatch failed for '{prompt}': {err}");
    }
}
