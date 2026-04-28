using Slasher.Automation;
using Slasher.Files;
using Slasher.Windows;

namespace Slasher.Api;

public static partial class SlasherEndpointExtensions
{
    private static void MapApplicationEndpoints(WebApplication app)
    {
        app.MapPost("/apps/start", (StartAppRequest request, WindowsAutomationService automation) =>
        {
            var result = automation.StartApp(request);
            return Results.Ok(result);
        });

        app.MapPost("/apps/select", (AppSelectRequest request, WindowsAutomationService automation) =>
        {
            var window = automation.SelectApp(request, out var error);
            return window is null
                ? Results.NotFound(error)
                : Results.Ok(window);
        });

        app.MapPost("/apps/close", (CloseProgramRequest request, WindowsAutomationService automation) =>
        {
            return Results.Ok(new { closed = automation.CloseProgram(request) });
        });
    }
}

