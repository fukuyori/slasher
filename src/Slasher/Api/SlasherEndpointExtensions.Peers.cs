using Slasher.Peers;

namespace Slasher.Api;

public static partial class SlasherEndpointExtensions
{
    private static void MapPeerEndpoints(WebApplication app)
    {
        app.MapGet("/peer/hello", (PeerEndpointService peers) =>
            Results.Ok(peers.GetHello()));

        app.MapGet("/peer/capabilities", (string? requestingPeerId, PeerEndpointService peers) =>
            Results.Ok(peers.GetCapabilities(requestingPeerId)));
    }
}
