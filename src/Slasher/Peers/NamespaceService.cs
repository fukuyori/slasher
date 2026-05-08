namespace Slasher.Peers;

public sealed class NamespaceService
{
    private readonly PeerRegistry _registry;

    public NamespaceService(PeerRegistry registry)
    {
        _registry = registry;
    }

    public NamespaceListResponse List(string? path, string? requestingPeerId)
    {
        var address = ResourceAddress.Parse(path);
        if (address.Segments.Count > 0)
        {
            return new NamespaceListResponse(1, address.Path, []);
        }

        var trustProfile = _registry.ResolveTrustProfile(requestingPeerId);
        var entries = new List<NamespaceEntry>
        {
            Entry("identity", "/identity", "peer.identity", "read"),
            Entry("capabilities", "/capabilities", "peer.capabilities", "read")
        };

        if (trustProfile >= PeerTrustProfile.Observed)
        {
            entries.Add(Entry("windows", "/windows", "window.collection", "list", "read"));
            entries.Add(Entry("screen", "/screen", "screen.collection", "list", "read"));
            entries.Add(Entry("runs", "/runs", "run.collection", "list", "read"));
            entries.Add(Entry("artifacts", "/artifacts", "artifact.collection", "list", "read"));
        }

        return new NamespaceListResponse(1, address.Path, entries);
    }

    private static NamespaceEntry Entry(string name, string path, string kind, params string[] operations)
    {
        return new NamespaceEntry(name, path, kind, operations);
    }
}
