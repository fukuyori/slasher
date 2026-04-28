using Slasher.Automation;
using Slasher.Files;
using Slasher.Windows;

namespace Slasher.Api;

public static partial class SlasherEndpointExtensions
{
    private static void MapScreenshotEndpoints(WebApplication app)
    {
        app.MapPost("/screenshot", (ScreenshotRequest request, WindowsAutomationService automation) =>
        {
            return automation.TakeScreenshot(request, out var screenshot, out var error)
                ? Results.Ok(screenshot)
                : Results.BadRequest(error);
        });
    }
}

