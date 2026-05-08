namespace Slasher.Peers;

public sealed record NamespaceEntry(
    string Name,
    string Path,
    string Kind,
    IReadOnlyList<string> Operations);

public sealed record NamespaceListResponse(
    int SchemaVersion,
    string Path,
    IReadOnlyList<NamespaceEntry> Entries);

public sealed record ResourceReadResponse(
    int SchemaVersion,
    string Path,
    string Kind,
    IReadOnlyList<string> CapabilitiesUsed,
    object? Value);
