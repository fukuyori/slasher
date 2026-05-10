using Slasher.Windows;

namespace Slasher.Api;

public static partial class SlasherEndpointExtensions
{
    private static void MapScreenEndpoints(WebApplication app)
    {
        app.MapGet("/screens", (WindowsAutomationService automation) =>
        {
            return Results.Ok(automation.ListScreens());
        });

        app.MapPost("/screen/image-match", (ImageMatchRequest request, WindowsAutomationService automation) =>
        {
            return automation.MatchImage(request, out var response, out var error)
                ? Results.Ok(response)
                : Results.BadRequest(error);
        });
    }
}
