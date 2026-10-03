using System.Collections.Immutable;

namespace Novolis.Hours.Client;

/// <summary>Preserves HTTP session cookies across API requests on any supported client transport.</summary>
public sealed class HoursSessionHandler : DelegatingHandler
{
    private readonly object gate = new();
    private ImmutableDictionary<string, string> cookies = ImmutableDictionary<string, string>.Empty;

    /// <summary>Initializes the handler over the supplied HTTP transport.</summary>
    public HoursSessionHandler(HttpMessageHandler innerHandler)
        : base(innerHandler ?? throw new ArgumentNullException(nameof(innerHandler)))
    {
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var cookieHeader = GetCookieHeader();
        if (!string.IsNullOrWhiteSpace(cookieHeader))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        }

        var response = await base.SendAsync(request, cancellationToken);
        StoreCookies(response);
        return response;
    }

    private string GetCookieHeader()
    {
        lock (gate)
        {
            return string.Join(
                "; ",
                cookies.OrderBy(cookie => cookie.Key, StringComparer.Ordinal)
                    .Select(cookie => $"{cookie.Key}={cookie.Value}"));
        }
    }

    private void StoreCookies(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return;
        }

        lock (gate)
        {
            foreach (var value in values)
            {
                var firstAttribute = value.IndexOf(';', StringComparison.Ordinal);
                var nameValue = firstAttribute >= 0 ? value[..firstAttribute] : value;
                var separator = nameValue.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                var name = nameValue[..separator].Trim();
                var cookieValue = nameValue[(separator + 1)..].Trim();
                cookies = string.IsNullOrEmpty(cookieValue)
                    ? cookies.Remove(name)
                    : cookies.SetItem(name, cookieValue);
            }
        }
    }
}
