using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Slasher.Peers;

public sealed class PeerRegistry
{
    private readonly IWebHostEnvironment _environment;
    private readonly PeerOptions _options;

    public PeerRegistry(IWebHostEnvironment environment, IOptions<PeerOptions> options)
    {
        _environment = environment;
        _options = options.Value;
    }

    public IReadOnlyList<PeerRegistryEntry> GetPeers()
    {
        var path = ResolvePath(_options.RegistryPath);
        if (path is null || !File.Exists(path))
        {
            return [];
        }

        var document = JsonSerializer.Deserialize<PeerRegistryDocument>(
            File.ReadAllText(path),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return document?.Peers ?? [];
    }

    public PeerRegistryEntry? Find(string peerId)
    {
        return GetPeers().FirstOrDefault(peer =>
            peer.Enabled && peer.PeerId.Equals(peerId, StringComparison.OrdinalIgnoreCase));
    }

    public PeerTrustProfile ResolveTrustProfile(string? peerId)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            return PeerTrustProfile.Known;
        }

        return Find(peerId)?.TrustProfile ?? PeerTrustProfile.Unknown;
    }

    private string? ResolvePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Path.IsPathRooted(path)
            ? path
            : Path.Combine(_environment.ContentRootPath, path);
    }
}
