namespace ManuscriptStudio.Core;

internal sealed class EditorSession
{
    public string? WorkspaceRoot { get; private set; }
    public string? SelectedFilePath { get; private set; }
    public string EditorText { get; set; } = string.Empty;
    public string LoadedSnapshot { get; private set; } = string.Empty;

    public bool IsDirty =>
        SelectedFilePath is not null && !string.Equals(EditorText, LoadedSnapshot, StringComparison.Ordinal);

    public void OpenWorkspace(string root)
    {
        var fullRoot = Path.GetFullPath(root);
        if (!Directory.Exists(fullRoot))
            throw new DirectoryNotFoundException($"Workspace folder not found: {fullRoot}");

        WorkspaceRoot = fullRoot;
        SelectedFilePath = null;
        EditorText = string.Empty;
        LoadedSnapshot = string.Empty;
    }

    public void SelectFile(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"File not found: {fullPath}");

        SelectedFilePath = fullPath;
        var text = File.ReadAllText(fullPath);
        EditorText = text;
        LoadedSnapshot = text;
    }

    public void SaveSelected()
    {
        if (SelectedFilePath is null)
            throw new InvalidOperationException("No file is selected.");

        File.WriteAllText(SelectedFilePath, EditorText);
        LoadedSnapshot = EditorText;
    }

    public int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
