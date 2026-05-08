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

        app.MapGet("/peer/ns", (string? path, string? requestingPeerId, NamespaceService peerNamespace) =>
        {
            try
            {
                return Results.Ok(peerNamespace.List(path, requestingPeerId));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ErrorResponse("peer_resource_path_invalid", ex.Message));
            }
        });

        app.MapGet("/peer/resource", (string? path, string? requestingPeerId, ResourceReadService resources) =>
        {
            try
            {
                return Results.Ok(resources.Read(path, requestingPeerId));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ErrorResponse("peer_resource_path_invalid", ex.Message));
            }
            catch (ResourceReadException ex)
            {
                return ex.Code == "peer_resource_not_found"
                    ? Results.NotFound(new ErrorResponse(ex.Code, ex.Message))
                    : Results.Json(new ErrorResponse(ex.Code, ex.Message), statusCode: StatusCodes.Status403Forbidden);
            }
        });
    }
}
