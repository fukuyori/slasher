using Slasher.Windows;

namespace Slasher.Api;

public static partial class SlasherEndpointExtensions
{
    private static void MapScreenEndpoints(WebApplication app)
    {
        app.MapPost("/screen/image-match", (ImageMatchRequest request, WindowsAutomationService automation) =>
        {
            return automation.MatchImage(request, out var response, out var error)
                ? Results.Ok(response)
                : Results.BadRequest(error);
        });
    }
}
