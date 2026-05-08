namespace Slasher.Peers;

public sealed record PeerHelloResponse(
    int SchemaVersion,
    string Protocol,
    int ProtocolVersion,
    string PeerId,
    string DisplayName,
    string ServerVersion,
    string? PublicKey,
    IReadOnlyList<string> Features);
