using Microsoft.Extensions.Options;

namespace Slasher.Peers;

public sealed class PeerEndpointService
{
    private readonly PeerIdentityStore _identityStore;
    private readonly PeerRegistry _registry;
    private readonly PeerOptions _options;

    public PeerEndpointService(
        PeerIdentityStore identityStore,
        PeerRegistry registry,
        IOptions<PeerOptions> options)
    {
        _identityStore = identityStore;
        _registry = registry;
        _options = options.Value;
    }

    public PeerHelloResponse GetHello()
    {
        var identity = _identityStore.Current;
        var version = typeof(PeerEndpointService).Assembly.GetName().Version?.ToString() ?? "unknown";
        return new PeerHelloResponse(
            SchemaVersion: 1,
            Protocol: PeerProtocol.Name,
            ProtocolVersion: PeerProtocol.Version,
            PeerId: identity.PeerId,
            DisplayName: identity.DisplayName,
            ServerVersion: version,
            PublicKey: identity.PublicKey,
            Features:
            [
                "peer-hello",
                "capability-negotiation"
            ]);
    }

    public PeerCapabilitiesResponse GetCapabilities(string? requestingPeerId)
    {
        var identity = _identityStore.Current;
        var trustProfile = _registry.ResolveTrustProfile(requestingPeerId);
        var capabilities = PeerCapabilities.All
            .Select(capability => ToStatus(capability, trustProfile))
            .ToArray();

        return new PeerCapabilitiesResponse(
            SchemaVersion: 1,
            PeerId: identity.PeerId,
            RequestingPeerId: requestingPeerId,
            TrustProfile: trustProfile,
            Capabilities: capabilities,
            Limits: new PeerCapabilityLimits(
                _options.MaxRunSeconds,
                _options.MaxArtifactBytes,
                RelayAllowed: false));
    }

    private static PeerCapabilityStatus ToStatus(string capability, PeerTrustProfile trustProfile)
    {
        if (capability is PeerCapabilities.Hello or PeerCapabilities.CapabilitiesRead)
        {
            return new PeerCapabilityStatus(capability, "allowed");
        }

        if (trustProfile < PeerTrustProfile.Observed)
        {
            return new PeerCapabilityStatus(capability, "denied", "trust_profile");
        }

        if (IsObserveCapability(capability))
        {
            return new PeerCapabilityStatus(capability, "allowed");
        }

        if (trustProfile >= PeerTrustProfile.Interactive && IsInteractiveCapability(capability))
        {
            return new PeerCapabilityStatus(capability, "requires-approval");
        }

        return new PeerCapabilityStatus(capability, "denied", "not_enabled");
    }

    private static bool IsObserveCapability(string capability)
    {
        return capability is PeerCapabilities.NamespaceRead
            or PeerCapabilities.ResourceRead
            or PeerCapabilities.ArtifactRead
            or PeerCapabilities.ObserveWindowList
            or PeerCapabilities.ObserveScreenCapture
            or PeerCapabilities.ObserveRunRead
            or PeerCapabilities.ObserveArtifactRead;
    }

    private static bool IsInteractiveCapability(string capability)
    {
        return capability is PeerCapabilities.ResourceInvoke
            or PeerCapabilities.RunDelegate
            or PeerCapabilities.RunCancel
            or PeerCapabilities.WindowFocus
            or PeerCapabilities.WindowMove
            or PeerCapabilities.InputText
            or PeerCapabilities.InputKeys
            or PeerCapabilities.InputMouse;
    }
}
