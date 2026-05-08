using System.Text.Json.Serialization;

namespace Slasher.Peers;

[JsonConverter(typeof(PeerTrustProfileJsonConverter))]
public enum PeerTrustProfile
{
    Unknown,
    Known,
    Observed,
    Interactive,
    Operator,
    AdminPeer
}
