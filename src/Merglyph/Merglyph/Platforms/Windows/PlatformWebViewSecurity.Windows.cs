using Microsoft.Maui.Handlers;

namespace Merglyph;

internal static partial class PlatformWebViewSecurity
{
    internal static partial void Configure()
    {
        WebViewHandler.Mapper.AppendToMapping("Merglyph.Security", static (handler, _) =>
        {
            var native = handler.PlatformView;
            native.CoreWebView2Initialized += (_, _) =>
            {
                if (native.CoreWebView2?.Settings is not { } settings)
                    return;

                settings.IsScriptEnabled = false;
                settings.IsWebMessageEnabled = false;
                settings.AreDevToolsEnabled = false;
                settings.AreDefaultContextMenusEnabled = false;
                settings.AreBrowserAcceleratorKeysEnabled = false;
                settings.IsStatusBarEnabled = false;
            };
        });
    }
}
