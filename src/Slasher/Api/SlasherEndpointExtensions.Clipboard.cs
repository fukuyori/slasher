using Slasher.Automation;
using Slasher.Files;
using Slasher.Windows;

namespace Slasher.Api;

public static partial class SlasherEndpointExtensions
{
    private static void MapClipboardEndpoints(WebApplication app)
    {
        app.MapGet("/clipboard", (ClipboardService clipboard) =>
        {
            return Results.Ok(new { text = clipboard.GetText() });
        });

        app.MapPost("/clipboard", (ClipboardTextRequest request, ClipboardService clipboard) =>
        {
            clipboard.SetText(request.Text);
            return Results.Ok(new { assigned = true, chars = request.Text.Length });
        });

        app.MapDelete("/clipboard", (ClipboardService clipboard) =>
        {
            clipboard.Clear();
            return Results.Ok(new { cleared = true });
        });
    }
}

