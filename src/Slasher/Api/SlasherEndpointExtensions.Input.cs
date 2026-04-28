using Slasher.Automation;
using Slasher.Files;
using Slasher.Windows;

namespace Slasher.Api;

public static partial class SlasherEndpointExtensions
{
    private static void MapInputEndpoints(WebApplication app)
    {
        app.MapPost("/input/keys", (KeyInputRequest request, WindowsAutomationService automation) =>
        {
            return automation.SendKeys(request, out var error)
                ? Results.Ok(new { sent = true })
                : Results.BadRequest(error);
        });

        app.MapPost("/input/text", (TextInputRequest request, WindowsAutomationService automation) =>
        {
            return automation.SendText(request, out var error)
                ? Results.Ok(new { sent = true, chars = request.Text.Length })
                : Results.BadRequest(error);
        });

        app.MapPost("/input/mouse", (MouseInputRequest request, WindowsAutomationService automation) =>
        {
            return automation.SendMouse(request, out var error)
                ? Results.Ok(new { sent = true })
                : Results.BadRequest(error);
        });

        app.MapPost("/input/mouse/drag", (MouseDragRequest request, WindowsAutomationService automation) =>
        {
            return automation.DragMouse(request, out var error)
                ? Results.Ok(new { dragged = true })
                : Results.BadRequest(error);
        });

        app.MapPost("/input/mouse/context-menu", (ContextMenuRequest request, WindowsAutomationService automation) =>
        {
            return automation.GetContextMenu(request, out var response, out var error)
                ? Results.Ok(response)
                : Results.BadRequest(error);
        });
    }
}

