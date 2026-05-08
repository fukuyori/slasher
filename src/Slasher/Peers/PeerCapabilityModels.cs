namespace Slasher.Peers;

public sealed record PeerCapabilityStatus(
    string Name,
    string Status,
    string? Reason = null);

public sealed record PeerCapabilitiesResponse(
    int SchemaVersion,
    string PeerId,
    string? RequestingPeerId,
    PeerTrustProfile TrustProfile,
    IReadOnlyList<PeerCapabilityStatus> Capabilities,
    PeerCapabilityLimits Limits);

public sealed record PeerCapabilityLimits(
    int MaxRunSeconds,
    long MaxArtifactBytes,
    bool RelayAllowed);
