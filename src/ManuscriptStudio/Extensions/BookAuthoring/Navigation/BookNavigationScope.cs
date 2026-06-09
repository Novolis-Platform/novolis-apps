namespace ManuscriptStudio.Extensions.BookAuthoring.Navigation;

internal enum BookNavigationScope
{
    All,
    Chapters,
    References,
}

internal static class BookNavigationScopeParser
{
    public static BookNavigationScope Parse(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "chapters" => BookNavigationScope.Chapters,
            "references" => BookNavigationScope.References,
            _ => BookNavigationScope.All,
        };

    public static string ToSettingValue(BookNavigationScope scope) =>
        scope switch
        {
            BookNavigationScope.Chapters => "chapters",
            BookNavigationScope.References => "references",
            _ => "all",
        };
}
