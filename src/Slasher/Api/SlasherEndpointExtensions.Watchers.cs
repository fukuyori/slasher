using Slasher.Files;

namespace Slasher.Api;

public static partial class SlasherEndpointExtensions
{
    private static void MapWatcherEndpoints(WebApplication app)
    {
        app.MapPost("/watchers/files", (FileWatcherStartRequest request, FileWatcherService watchers) =>
        {
            try
            {
                return Results.Ok(new FileWatcherStartResponse(watchers.Start(request)));
            }
            catch (DirectoryNotFoundException ex)
            {
                return Results.BadRequest(new ErrorResponse("watch_path_not_found", ex.Message));
            }
        });

        app.MapGet("/watchers/files", (FileWatcherService watchers) =>
            Results.Ok(new FileWatcherListResponse(watchers.List())));

        app.MapGet("/watchers/files/{watcherId}/events", (string watcherId, int? limit, FileWatcherService watchers) =>
        {
            try
            {
                return Results.Ok(new FileWatcherEventsResponse(watcherId, watchers.GetEvents(watcherId, limit)));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new ErrorResponse("watcher_not_found", ex.Message));
            }
        });

        app.MapPost("/watchers/files/{watcherId}/stop", (string watcherId, FileWatcherService watchers) =>
        {
            var stopped = watchers.Stop(watcherId);
            return stopped
                ? Results.Ok(new FileWatcherStopResponse(watcherId, true))
                : Results.NotFound(new ErrorResponse("watcher_not_found", $"Watcher '{watcherId}' was not found."));
        });
    }
}
