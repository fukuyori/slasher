using Slasher.Automation;
using Slasher.Files;
using Slasher.Windows;

namespace Slasher.Api;

public static partial class SlasherEndpointExtensions
{
    private static void MapWindowEndpoints(WebApplication app)
    {
        app.MapGet("/windows", (string? title, int? processId, WindowsAutomationService automation) =>
        {
            return Results.Ok(automation.ListWindows(title, processId));
        });

        app.MapGet("/windows/foreground", (WindowsAutomationService automation) =>
        {
            return automation.TryGetForegroundWindow(out var window)
                ? Results.Ok(window)
                : Results.NotFound(new ErrorResponse("foreground_window_not_found", "No foreground window was found."));
        });

        app.MapGet("/windows/foreground/title", (WindowsAutomationService automation) =>
        {
            return Results.Ok(new { title = automation.GetActiveWindowTitle() ?? string.Empty });
        });

        app.MapPost("/windows/activate", (WindowQueryRequest request, WindowsAutomationService automation) =>
        {
            var window = automation.FindWindow(request);
            if (window is null)
            {
                return Results.NotFound(new ErrorResponse("window_not_found", "No matching window was found."));
            }

            return automation.FocusWindow(window.Handle, out var error)
                ? Results.Ok(window)
                : Results.BadRequest(error);
        });

        app.MapPost("/windows/wait", async (WindowQueryRequest request, WindowsAutomationService automation, CancellationToken cancellationToken) =>
        {
            var window = await automation.WaitForWindowAsync(request, cancellationToken);
            return window is null
                ? Results.NotFound(new ErrorResponse("window_timeout", "Timed out waiting for a matching window."))
                : Results.Ok(window);
        });

        app.MapPost("/windows/close-all", (CloseAllWindowsRequest request, WindowsAutomationService automation) =>
        {
            return Results.Ok(new { closed = automation.CloseAllWindows(request) });
        });

        app.MapGet("/windows/{handle}", (string handle, WindowsAutomationService automation) =>
        {
            return automation.TryGetWindow(handle, out var window)
                ? Results.Ok(window)
                : Results.NotFound(new ErrorResponse("window_not_found", $"Window '{handle}' was not found."));
        });

        app.MapPost("/windows/{handle}/focus", (string handle, WindowsAutomationService automation) =>
        {
            return automation.FocusWindow(handle, out var error)
                ? Results.Ok(new { focused = true, handle })
                : Results.BadRequest(error);
        });

        app.MapPost("/windows/{handle}/close", (string handle, WindowsAutomationService automation) =>
        {
            return automation.CloseWindow(handle, out var error)
                ? Results.Ok(new { closed = true, handle })
                : Results.BadRequest(error);
        });

        app.MapPost("/windows/{handle}/move", (string handle, MoveWindowRequest request, WindowsAutomationService automation) =>
        {
            return automation.MoveWindow(handle, request, out var error)
                ? Results.Ok(new { moved = true, handle })
                : Results.BadRequest(error);
        });

        app.MapPost("/windows/{handle}/state", (string handle, WindowStateRequest request, WindowsAutomationService automation) =>
        {
            return automation.SetWindowState(handle, request, out var error)
                ? Results.Ok(new { state = request.State, handle })
                : Results.BadRequest(error);
        });
    }
}

