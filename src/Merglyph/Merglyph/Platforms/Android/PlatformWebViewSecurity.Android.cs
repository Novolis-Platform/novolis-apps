using Android.Webkit;
using Microsoft.Maui.Handlers;

namespace Merglyph;

internal static partial class PlatformWebViewSecurity
{
    internal static partial void Configure()
    {
        WebViewHandler.Mapper.AppendToMapping("Merglyph.Security", static (handler, _) =>
        {
            var settings = handler.PlatformView.Settings;
            settings.JavaScriptEnabled = false;
            settings.DomStorageEnabled = false;
            settings.DatabaseEnabled = false;
            settings.AllowFileAccess = false;
            settings.AllowContentAccess = false;
            settings.BlockNetworkLoads = true;
            settings.MixedContentMode = MixedContentHandling.NeverAllow;
            settings.SetSupportMultipleWindows(false);
        });
    }
}
