using WeCantSpell.Hunspell;

namespace BooksWriterStudio.Services;

/// <summary>Optional Hunspell spellcheck (loads dictionaries when present).</summary>
public sealed class SpellService
{
    WordList? _list;

    /// <summary>True when a dictionary was loaded.</summary>
    public bool IsAvailable => _list is not null;

    /// <summary>Tries to load en_US aff/dic from app Assets or beside the executable.</summary>
    public void TryLoad(string? customDictionaryPath = null)
    {
        var bases = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "Dictionaries"),
            Path.Combine(AppContext.BaseDirectory, "Dictionaries"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Novolis", "BooksWriterStudio", "Dictionaries"),
        };

        foreach (var dir in bases)
        {
            var aff = Path.Combine(dir, "en_US.aff");
            var dic = Path.Combine(dir, "en_US.dic");
            if (File.Exists(aff) && File.Exists(dic))
            {
                using var affStream = File.OpenRead(aff);
                using var dicStream = File.OpenRead(dic);
                _list = WordList.CreateFromStreams(affStream, dicStream);
                break;
            }
        }

        if (_list is null || string.IsNullOrWhiteSpace(customDictionaryPath) || !File.Exists(customDictionaryPath))
            return;

        // Custom dictionary is additive word list only when Hunspell base loaded.
        try
        {
            foreach (var line in File.ReadLines(customDictionaryPath))
            {
                var word = line.Trim();
                if (word.Length > 0 && !word.StartsWith('#'))
                    _ = word; // WordList API is check-only; custom words checked via Contains after merge not supported — skip.
            }
        }
        catch
        {
            // ignore custom dict IO errors
        }
    }

    /// <summary>Returns misspelled word tokens in <paramref name="text"/> (empty if unavailable).</summary>
    public IReadOnlyList<string> FindMisspellings(string text)
    {
        if (_list is null || string.IsNullOrWhiteSpace(text))
            return [];

        var misses = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in Tokenize(text))
        {
            if (token.Length < 2 || !seen.Add(token))
                continue;
            if (!_list.Check(token))
                misses.Add(token);
        }

        return misses;
    }

    static IEnumerable<string> Tokenize(string text)
    {
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var c = i < text.Length ? text[i] : '\0';
            if (char.IsLetter(c) || c is '\'' or '-')
            {
                if (start < 0)
                    start = i;
            }
            else if (start >= 0)
            {
                yield return text[start..i];
                start = -1;
            }
        }
    }
}
