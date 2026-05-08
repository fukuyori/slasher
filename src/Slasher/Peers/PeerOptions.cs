namespace Slasher.Peers;

public sealed class PeerOptions
{
    public bool Enabled { get; set; }

    public string? IdentityPath { get; set; }

    public string? RegistryPath { get; set; }

    public string? DisplayName { get; set; }

    public int MaxRunSeconds { get; set; } = 300;

    public long MaxArtifactBytes { get; set; } = 100 * 1024 * 1024;
}
