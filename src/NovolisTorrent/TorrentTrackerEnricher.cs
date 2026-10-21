using Novolis.Transports.Torrent.BEncoding;

namespace Novolis.Torrent;

internal static class TorrentTrackerEnricher
{
    public static Uri? AddToMagnet(
        Uri? magnetUri,
        IEnumerable<Uri> trackers)
    {
        ArgumentNullException.ThrowIfNull(trackers);
        if (magnetUri is null
            || !string.Equals(
                magnetUri.Scheme,
                "magnet",
                StringComparison.OrdinalIgnoreCase))
        {
            return magnetUri;
        }

        var supported = Normalize(trackers);
        if (supported.Count == 0)
            return magnetUri;

        var query = magnetUri.Query.TrimStart('?');
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in query.Split(
                     '&',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = parameter.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = Uri.UnescapeDataString(parameter[..separator]);
            if (!string.Equals(key, "tr", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = Uri.UnescapeDataString(parameter[(separator + 1)..]);
            if (!string.IsNullOrWhiteSpace(value))
                existing.Add(value);
        }

        var additions = supported
            .Where(tracker => existing.Add(tracker.AbsoluteUri))
            .Select(tracker => "tr=" + Uri.EscapeDataString(tracker.AbsoluteUri))
            .ToArray();
        if (additions.Length == 0)
            return magnetUri;

        var parameters = new List<string>();
        if (!string.IsNullOrWhiteSpace(query))
            parameters.Add(query);
        parameters.AddRange(additions);

        var builder = new UriBuilder(magnetUri)
        {
            Query = string.Join("&", parameters),
        };
        return builder.Uri;
    }

    public static byte[] AddToTorrent(
        byte[] torrentBytes,
        IEnumerable<Uri> trackers)
    {
        ArgumentNullException.ThrowIfNull(torrentBytes);
        ArgumentNullException.ThrowIfNull(trackers);

        var supported = Normalize(trackers);
        if (supported.Count == 0)
            return torrentBytes;

        var dictionary = BEncodedDictionary.DecodeTorrent(torrentBytes);
        var announceKey = new BEncodedString("announce");
        var announceListKey = new BEncodedString("announce-list");
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (dictionary.TryGetValue(announceKey, out var announce)
            && announce is BEncodedString announceString)
        {
            existing.Add(announceString.Text);
        }

        var announceList = dictionary.TryGetValue(
                announceListKey,
                out var announceListValue)
            && announceListValue is BEncodedList existingList
            ? existingList
            : new BEncodedList();

        foreach (var tier in announceList.OfType<BEncodedList>())
        {
            foreach (var value in tier.OfType<BEncodedString>())
                existing.Add(value.Text);
        }

        var additions = supported
            .Where(tracker => existing.Add(tracker.AbsoluteUri))
            .ToArray();
        if (additions.Length == 0
            && dictionary.ContainsKey(announceKey)
            && dictionary.ContainsKey(announceListKey))
        {
            return torrentBytes;
        }

        if (!dictionary.ContainsKey(announceKey))
        {
            var first = additions.FirstOrDefault();
            if (first is null)
                return torrentBytes;

            dictionary[announceKey] = new BEncodedString(first.AbsoluteUri);
        }

        if (!dictionary.ContainsKey(announceListKey))
        {
            var announceValue = dictionary[announceKey] as BEncodedString;
            if (announceValue is not null)
            {
                announceList.Add(
                    new BEncodedList(
                    [
                        new BEncodedString(announceValue.Text),
                    ]));
            }
        }

        if (additions.Length > 0)
        {
            announceList.Add(
                new BEncodedList(
                    additions.Select(
                        tracker => (BEncodedValue)new BEncodedString(
                            tracker.AbsoluteUri))));
        }

        dictionary[announceListKey] = announceList;
        return dictionary.Encode();
    }

    private static IReadOnlyList<Uri> Normalize(IEnumerable<Uri> trackers) =>
        trackers
            .Where(
                tracker => tracker is not null
                    && tracker.IsAbsoluteUri
                    && tracker.Scheme is "http" or "https" or "udp")
            .GroupBy(
                tracker => tracker.AbsoluteUri,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
}
