namespace Novolis.Hours.Client.Presentation;

/// <summary>One calendar layer as shown beside the day strip.</summary>
public sealed record LayerRailRow(
    string Kind,
    int Order,
    string Said,
    bool Speaking);
