using Slasher.Automation;
using Slasher.Windows;

namespace Slasher.Peers;

public sealed class ResourceReadService
{
    private readonly PeerEndpointService _peerEndpointService;
    private readonly PeerIdentityStore _identityStore;
    private readonly PeerRegistry _registry;
    private readonly WindowsAutomationService _windows;
    private readonly AutomationRunArtifactStore _artifactStore;

    public ResourceReadService(
        PeerEndpointService peerEndpointService,
        PeerIdentityStore identityStore,
        PeerRegistry registry,
        WindowsAutomationService windows,
        AutomationRunArtifactStore artifactStore)
    {
        _peerEndpointService = peerEndpointService;
        _identityStore = identityStore;
        _registry = registry;
        _windows = windows;
        _artifactStore = artifactStore;
    }

    public ResourceReadResponse Read(string? path, string? requestingPeerId)
    {
        var address = ResourceAddress.Parse(path);
        var trustProfile = _registry.ResolveTrustProfile(requestingPeerId);

        if (address.Segments.Count == 0)
        {
            return new ResourceReadResponse(1, "/", "namespace.root", [PeerCapabilities.NamespaceRead], null);
        }

        return address.Segments[0] switch
        {
            "identity" when address.Segments.Count == 1 => new ResourceReadResponse(
                1,
                address.Path,
                "peer.identity",
                [PeerCapabilities.Hello],
                _identityStore.Current),
            "capabilities" when address.Segments.Count == 1 => new ResourceReadResponse(
                1,
                address.Path,
                "peer.capabilities",
                [PeerCapabilities.CapabilitiesRead],
                _peerEndpointService.GetCapabilities(requestingPeerId)),
            "windows" => ReadWindows(address, trustProfile),
            "screen" => ReadScreen(address, trustProfile),
            "runs" => ReadRuns(address, trustProfile),
            "artifacts" => ReadArtifact(address, trustProfile),
            _ => throw new ResourceReadException("peer_resource_not_found", $"Resource '{address.Path}' was not found.")
        };
    }

    private ResourceReadResponse ReadWindows(ResourceAddress address, PeerTrustProfile trustProfile)
    {
        RequireObserved(trustProfile, PeerCapabilities.ObserveWindowList);
        if (address.Segments.Count == 1)
        {
            return new ResourceReadResponse(
                1,
                address.Path,
                "window.collection",
                [PeerCapabilities.ObserveWindowList],
                _windows.ListWindows(null, null));
        }

        if (address.Segments.Count == 2 && _windows.TryGetWindow(address.Segments[1], out var window) && window is not null)
        {
            return new ResourceReadResponse(
                1,
                address.Path,
                "window.record",
                [PeerCapabilities.ObserveWindowList],
                window);
        }

        throw new ResourceReadException("peer_resource_not_found", $"Window resource '{address.Path}' was not found.");
    }

    private static ResourceReadResponse ReadScreen(ResourceAddress address, PeerTrustProfile trustProfile)
    {
        RequireObserved(trustProfile, PeerCapabilities.ObserveScreenCapture);
        if (address.Segments.Count is 1 || (address.Segments.Count == 2 && address.Segments[1] == "primary"))
        {
            return new ResourceReadResponse(
                1,
                address.Path,
                address.Segments.Count == 1 ? "screen.collection" : "screen",
                [PeerCapabilities.ObserveScreenCapture],
                new { capture = "invoke-only", target = address.Segments.Count == 1 ? null : "primary" });
        }

        throw new ResourceReadException("peer_resource_not_found", $"Screen resource '{address.Path}' was not found.");
    }

    private ResourceReadResponse ReadRuns(ResourceAddress address, PeerTrustProfile trustProfile)
    {
        RequireObserved(trustProfile, PeerCapabilities.ObserveRunRead);
        if (address.Segments.Count == 1)
        {
            return new ResourceReadResponse(
                1,
                address.Path,
                "run.collection",
                [PeerCapabilities.ObserveRunRead],
                _artifactStore.ListRuns());
        }

        if (address.Segments.Count == 2 && _artifactStore.TryReadRun(address.Segments[1], out var report) && report is not null)
        {
            return new ResourceReadResponse(
                1,
                address.Path,
                "run.record",
                [PeerCapabilities.ObserveRunRead],
                report);
        }

        throw new ResourceReadException("peer_resource_not_found", $"Run resource '{address.Path}' was not found.");
    }

    private ResourceReadResponse ReadArtifact(ResourceAddress address, PeerTrustProfile trustProfile)
    {
        RequireObserved(trustProfile, PeerCapabilities.ObserveArtifactRead);
        if (address.Segments.Count < 4 || address.Segments[1] != "runs")
        {
            throw new ResourceReadException("peer_resource_not_found", $"Artifact resource '{address.Path}' was not found.");
        }

        var runId = address.Segments[2];
        var relativePath = string.Join('/', address.Segments.Skip(3));
        if (!_artifactStore.TryReadArtifactContent(runId, relativePath, out var content) || content is null)
        {
            throw new ResourceReadException("peer_resource_not_found", $"Artifact resource '{address.Path}' was not found.");
        }

        return new ResourceReadResponse(
            1,
            address.Path,
            "artifact",
            [PeerCapabilities.ObserveArtifactRead],
            content);
    }

    private static void RequireObserved(PeerTrustProfile trustProfile, string capability)
    {
        if (trustProfile < PeerTrustProfile.Observed)
        {
            throw new ResourceReadException(
                "peer_capability_denied",
                $"Trust profile '{trustProfile}' is not allowed to read capability '{capability}'.");
        }
    }
}

public sealed class ResourceReadException : Exception
{
    public ResourceReadException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
