namespace Merglyph.Core;

public readonly record struct DocumentName
{
    public DocumentName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = Path.GetFileName(value.Trim());
        if (string.IsNullOrWhiteSpace(Value))
            throw new ArgumentException("Document name must contain a file name.", nameof(value));
    }

    public string Value { get; }
    public override string ToString() => Value;
}
