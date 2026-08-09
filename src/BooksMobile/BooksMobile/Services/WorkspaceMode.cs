namespace BooksMobile.Services;

/// <summary>Which GitHub content tree Books Mobile is browsing.</summary>
public enum WorkspaceMode
{
    /// <summary>frankhaugen/books — Novolis Manuscript Protocol (NMP/1) under src/.</summary>
    Books = 0,

    /// <summary>frankhaugen/galactic-confederation-review — MkDocs docs/ selections.</summary>
    Review = 1,
}
