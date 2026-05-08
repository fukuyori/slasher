namespace Slasher.Peers;

public sealed record PeerIdentity(
    int SchemaVersion,
    string PeerId,
    string DisplayName,
    string? PublicKey = null,
    string? Owner = null,
    DateTimeOffset? CreatedAt = null);

public sealed record PeerRegistryDocument(
    int SchemaVersion,
    IReadOnlyList<PeerRegistryEntry> Peers);

public sealed record PeerRegistryEntry(
    string PeerId,
    string DisplayName,
    string? BaseUrl = null,
    string? PublicKey = null,
    PeerTrustProfile TrustProfile = PeerTrustProfile.Known,
    bool Enabled = true);
