namespace Novolis.NMule;

/// <summary>One selectable eD2K server row shown by the nMule host.</summary>
public sealed record NmuleServerRow(
    string Name,
    string Endpoint,
    string Summary,
    string Host,
    int Port);
