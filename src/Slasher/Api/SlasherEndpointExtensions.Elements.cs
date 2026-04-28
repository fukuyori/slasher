using Slasher.Windows;

namespace Slasher.Api;

public static partial class SlasherEndpointExtensions
{
    private static void MapElementEndpoints(WebApplication app)
    {
        app.MapGet("/elements/tree", (
            string? handle,
            int? maxDepth,
            int? maxChildren,
            WindowsAutomationService automation) =>
        {
            return automation.GetElementTree(handle, maxDepth ?? 3, maxChildren ?? 200, out var tree, out var error)
                ? Results.Ok(tree)
                : Results.BadRequest(error);
        });

        app.MapGet("/windows/{handle}/elements", (
            string handle,
            int? maxDepth,
            int? maxChildren,
            WindowsAutomationService automation) =>
        {
            return automation.GetElementTree(handle, maxDepth ?? 3, maxChildren ?? 200, out var tree, out var error)
                ? Results.Ok(tree)
                : Results.BadRequest(error);
        });

        app.MapGet("/elements/find", (
            string? handle,
            string? title,
            string? className,
            int? controlId,
            string? match,
            int? maxDepth,
            int? maxResults,
            WindowsAutomationService automation) =>
        {
            return automation.FindElements(
                handle,
                title,
                className,
                controlId,
                match ?? "contains",
                maxDepth ?? 8,
                maxResults ?? 20,
                out var response,
                out var error)
                ? Results.Ok(response)
                : Results.BadRequest(error);
        });

        app.MapGet("/windows/{handle}/elements/find", (
            string handle,
            string? title,
            string? className,
            int? controlId,
            string? match,
            int? maxDepth,
            int? maxResults,
            WindowsAutomationService automation) =>
        {
            return automation.FindElements(
                handle,
                title,
                className,
                controlId,
                match ?? "contains",
                maxDepth ?? 8,
                maxResults ?? 20,
                out var response,
                out var error)
                ? Results.Ok(response)
                : Results.BadRequest(error);
        });

        app.MapPost("/elements/click", (ElementClickRequest request, WindowsAutomationService automation) =>
        {
            return automation.ClickElement(request, out var element, out var error)
                ? Results.Ok(new { clicked = true, element })
                : Results.BadRequest(error);
        });

        app.MapGet("/elements/exists", (
            string? handle,
            string? title,
            string? className,
            int? controlId,
            string? match,
            int? maxDepth,
            WindowsAutomationService automation) =>
        {
            var request = new ElementClickRequest(handle, title, className, controlId, match ?? "contains", maxDepth ?? 8);
            return automation.ElementExists(request, out var response, out var error)
                ? Results.Ok(response)
                : Results.BadRequest(error);
        });

        app.MapGet("/elements/text", (
            string? handle,
            string? title,
            string? className,
            int? controlId,
            string? match,
            int? maxDepth,
            WindowsAutomationService automation) =>
        {
            var request = new ElementClickRequest(handle, title, className, controlId, match ?? "contains", maxDepth ?? 8);
            return automation.GetElementText(request, out var response, out var error)
                ? Results.Ok(response)
                : Results.BadRequest(error);
        });
    }
}
