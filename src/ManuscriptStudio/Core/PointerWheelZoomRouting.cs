using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Novolis.Avalonia.Markdown;

namespace ManuscriptStudio.Core;

/// <summary>Tunnel-route Ctrl+wheel zoom so it wins over scroll viewers and AvalonEdit.</summary>
internal static class PointerWheelZoomRouting
{
    public static void Attach(InputElement element, Func<double> getScale, Action<double> setScale)
    {
        element.AddHandler(
            InputElement.PointerWheelChangedEvent,
            (_, e) =>
            {
                if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
                    return;

                var next = MarkdownZoom.ApplyWheelDelta(getScale(), e.Delta.Y);
                if (Math.Abs(next - getScale()) < 0.0001)
                    return;

                setScale(next);
                e.Handled = true;
            },
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
    }
}
