using System.Net.Http;

namespace ReadAloud.Reading;

/// <summary>Turns Azure/MSAL failures into short reader-facing copy.</summary>
internal static class AzureErrorText
{
    public static string Describe(Exception ex, string fallbackPrefix)
    {
        ArgumentNullException.ThrowIfNull(ex);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackPrefix);

        if (ex is OperationCanceledException)
            return "Cancelled.";

        var typeName = ex.GetType().Name;
        var message = ex.Message ?? string.Empty;

        if (Contains(typeName, "Msal") || Contains(message, "authentication_canceled"))
        {
            if (Contains(message, "authentication_canceled") || Contains(message, "User canceled"))
                return "Microsoft sign-in was cancelled.";
            if (Contains(message, "access_denied"))
                return "Microsoft sign-in was denied for this account.";
            if (Contains(typeName, "UiRequired") || Contains(message, "interaction_required"))
                return "Microsoft needs another sign-in. Tap Sign in with Microsoft again.";
            return "Microsoft sign-in failed. Try again, or pick a different account.";
        }

        if (ex is HttpRequestException)
            return "Could not reach Azure. Check the network and try again.";

        if (ex is UnauthorizedAccessException)
            return "This Microsoft account cannot access that Azure resource.";

        if (ex is ArgumentException && !string.IsNullOrWhiteSpace(ex.Message))
            return Trim(ex.Message);

        return string.IsNullOrWhiteSpace(ex.Message)
            ? fallbackPrefix
            : $"{fallbackPrefix}: {Trim(ex.Message)}";
    }

    static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    static string Trim(string message)
    {
        var text = message.Trim();
        const int max = 180;
        return text.Length <= max ? text : text[..max].TrimEnd() + "…";
    }
}
