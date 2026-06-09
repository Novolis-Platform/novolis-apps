using ManuscriptStudio.Extensions.BookAuthoring;
using ManuscriptStudio.Extensions.GenericMarkdown;

namespace ManuscriptStudio.Core;

internal sealed class ManuscriptExtensionRegistry
{
    private readonly IReadOnlyList<IManuscriptExtension> _extensions;

    public ManuscriptExtensionRegistry()
    {
        _extensions = [
            new GenericMarkdownExtension(),
            new BookAuthoringExtension(),
        ];
    }

    public IReadOnlyList<IManuscriptExtension> All => _extensions;

    public IManuscriptExtension GetById(string id) =>
        _extensions.FirstOrDefault(e => e.Id == id) ?? _extensions[0];

    public IManuscriptExtension GetActive(ManuscriptSettingsStore settings) =>
        GetById(settings.Settings.ActiveExtensionId);
}
