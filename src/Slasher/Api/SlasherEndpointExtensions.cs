using Slasher.Automation;
using Slasher.Files;
using Slasher.Windows;

namespace Slasher.Api;

public static partial class SlasherEndpointExtensions
{
    public static WebApplication MapSlasherEndpoints(this WebApplication app)
    {
        app.MapGet("/api", () => Results.Ok(new
        {
            name = "Slasher",
            status = "ready",
            endpoints = new[]
            {
                "GET /health",
                "POST /apps/start",
                "POST /apps/select",
                "GET /windows",
                "GET /windows/foreground",
                "GET /windows/{handle}",
                "GET /windows/{handle}/elements",
                "GET /windows/{handle}/elements/find",
                "GET /elements/tree",
                "GET /elements/find",
                "GET /elements/exists",
                "GET /elements/text",
                "POST /elements/click",
                "POST /browser/open",
                "POST /browser/navigate",
                "GET /browser/current",
                "GET /browser/title",
                "GET /browser/url",
                "POST /browser/find",
                "POST /browser/click",
                "POST /browser/hover",
                "POST /browser/double-click",
                "POST /browser/right-click",
                "POST /browser/type",
                "POST /browser/press",
                "POST /browser/upload",
                "POST /browser/drag",
                "POST /browser/select-option",
                "POST /browser/selected-options",
                "POST /browser/clear",
                "POST /browser/submit",
                "POST /browser/text",
                "POST /browser/attribute",
                "POST /browser/wait",
                "POST /browser/wait-text",
                "POST /browser/js",
                "GET /browser/cookies",
                "POST /browser/storage/{storage}/get",
                "POST /browser/storage/{storage}/set",
                "POST /browser/screenshot",
                "GET /browser/links",
                "GET /browser/windows",
                "POST /browser/new-window",
                "POST /browser/switch-window",
                "POST /browser/close-window",
                "POST /browser/close",
                "POST /browser/downloads/wait",
                "GET /browser/logs",
                "POST /windows/{handle}/focus",
                "POST /windows/{handle}/close",
                "POST /windows/{handle}/move",
                "POST /windows/{handle}/state",
                "POST /input/keys",
                "POST /input/text",
                "POST /input/mouse",
                "POST /screenshot",
                "POST /screen/image-match",
                "POST /automation/runs",
                "GET /automation/runs",
                "GET /automation/runs/{runId}",
                "GET /automation/runs/{runId}/events",
                "GET /automation/runs/{runId}/summary",
                "GET /automation/runs/{runId}/logs/script",
                "GET /automation/runs/{runId}/report",
                "GET /automation/runs/{runId}/artifacts/raw",
                "GET /automation/runs/{runId}/artifacts/content",
                "POST /scripts/check",
                "POST /scripts/run",
                "POST /scripts/run-file"
            }
        }));

        app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTimeOffset.UtcNow }));

        MapApplicationEndpoints(app);
        MapWindowEndpoints(app);
        MapElementEndpoints(app);
        MapBrowserEndpoints(app);
        MapInputEndpoints(app);
        MapScreenEndpoints(app);
        MapClipboardEndpoints(app);
        MapFileSystemEndpoints(app);
        MapScreenshotEndpoints(app);
        MapAutomationEndpoints(app);
        MapScriptEndpoints(app);

        return app;
    }
}

