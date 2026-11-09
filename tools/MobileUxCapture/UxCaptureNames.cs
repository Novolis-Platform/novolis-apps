namespace MobileUxCapture;

internal static class UxCaptureNames
{
    public static string File(string app, string view, string form, string theme) =>
        $"{app}-{view}-{form}-{theme}.png";
}
