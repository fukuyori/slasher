using System.Text.Json;
using System.Text.Json.Serialization;

namespace Slasher.Peers;

public sealed class PeerTrustProfileJsonConverter : JsonConverter<PeerTrustProfile>
{
    public override PeerTrustProfile Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value?.ToLowerInvariant() switch
        {
            "unknown" => PeerTrustProfile.Unknown,
            "known" => PeerTrustProfile.Known,
            "observed" => PeerTrustProfile.Observed,
            "interactive" => PeerTrustProfile.Interactive,
            "operator" => PeerTrustProfile.Operator,
            "admin-peer" => PeerTrustProfile.AdminPeer,
            "adminpeer" => PeerTrustProfile.AdminPeer,
            _ => throw new JsonException($"Unknown peer trust profile '{value}'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, PeerTrustProfile value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            PeerTrustProfile.Unknown => "unknown",
            PeerTrustProfile.Known => "known",
            PeerTrustProfile.Observed => "observed",
            PeerTrustProfile.Interactive => "interactive",
            PeerTrustProfile.Operator => "operator",
            PeerTrustProfile.AdminPeer => "admin-peer",
            _ => throw new JsonException($"Unknown peer trust profile '{value}'.")
        });
    }
}
