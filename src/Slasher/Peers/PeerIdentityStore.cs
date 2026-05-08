using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Slasher.Peers;

public sealed class PeerIdentityStore
{
    private readonly IWebHostEnvironment _environment;
    private readonly PeerOptions _options;
    private readonly Lazy<PeerIdentity> _identity;

    public PeerIdentityStore(IWebHostEnvironment environment, IOptions<PeerOptions> options)
    {
        _environment = environment;
        _options = options.Value;
        _identity = new Lazy<PeerIdentity>(Load);
    }

    public PeerIdentity Current => _identity.Value;

    private PeerIdentity Load()
    {
        var path = ResolvePath(_options.IdentityPath);
        if (path is not null && File.Exists(path))
        {
            var document = JsonSerializer.Deserialize<PeerIdentity>(
                File.ReadAllText(path),
                JsonOptions());
            if (document is not null && !string.IsNullOrWhiteSpace(document.PeerId))
            {
                return document;
            }
        }

        var identity = new PeerIdentity(
            SchemaVersion: 1,
            PeerId: $"peer_{Guid.NewGuid():N}",
            DisplayName: string.IsNullOrWhiteSpace(_options.DisplayName)
                ? Environment.MachineName
                : _options.DisplayName!,
            CreatedAt: DateTimeOffset.UtcNow);

        if (path is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, JsonSerializer.Serialize(identity, JsonOptions()));
        }

        return identity;
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

    private static JsonSerializerOptions JsonOptions()
    {
        return new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
    }
}
