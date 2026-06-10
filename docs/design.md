# Design

## Purpose

`novolis-apps` hosts shipped desktop applications that consume published Novolis packages from GitHub Packages. Each app under `src/` is self-contained.

## Non-goals

- Publishing NuGet packages from this repository
- In-repo shared libraries or cross-app `ProjectReference`
- Cross-repo `ProjectReference` into sibling `novolis-*` clones

## Manuscript Studio

Single WinExe with a **built-in extension host** (no dynamic plugin DLLs):

| Mode | Id | Role |
|------|-----|------|
| Generic Markdown | `generic-markdown` | Folder tree + Markdig preview |
| Book Authoring | `book-authoring` | Series/book/chapter nav, metadata helpers, books-aligned preview, QuestPDF export |
| Concept Assets | `concept-assets` | Preview Concept Studio PNG exports linked from a workspace folder |

Book Authoring behavior is informed by the editorial layout in a local publishing workspace (`content/series/`, `book.yaml`, `[!tag]` metadata). That workspace is **reference material for authors** — Manuscript Studio does not depend on the books git repository at build time.

All book logic (`ContentCatalog`, `ChapterMetadata`, `BookPdfExporter`) lives inside `src/ManuscriptStudio/Extensions/BookAuthoring/`.

## Concept Studio

WinExe for simple 3D concept blockout (ship hulls, props) with CAD-style materials, orthographic views, dimension annotations, and SVG/PNG export.

- Path: `src/ConceptStudio/`
- Data: `%LocalAppData%\Novolis\Concept Studio\default-workspace\concept.json`
- Consumes `Novolis.Avalonia.Raylib`, `Novolis.Rendering.*` from GitHub Packages (NuGet-only)

## Package sources

| Source | URL |
|--------|-----|
| GitHub Packages | `Novolis.Avalonia.Studio` and other `Novolis.*` |
| nuget.org | Avalonia, Markdig, QuestPDF, YamlDotNet |

## Related

- [getting-started.md](getting-started.md)
- [release.md](release.md)
- [apps-repos.md](../../novolis-governance/docs/apps-repos.md)
