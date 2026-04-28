using Slasher.Automation;
using Slasher.Files;
using Slasher.Windows;

namespace Slasher.Api;

public static partial class SlasherEndpointExtensions
{
    private static void MapScriptEndpoints(WebApplication app)
    {
        app.MapPost("/scripts/check", async (
            ScriptCheckRequest request,
            ScriptRunService scriptRunner,
            CancellationToken cancellationToken) =>
        {
            var response = await scriptRunner.CheckAsync(request, cancellationToken);
            return response.Ok
                ? Results.Ok(response)
                : Results.BadRequest(response);
        });

        app.MapPost("/scripts/run", async (
            ScriptRunRequest request,
            ScriptRunService scriptRunner,
            CancellationToken cancellationToken) =>
        {
            var response = await scriptRunner.RunAsync(request, cancellationToken);
            return response.Ok
                ? Results.Ok(response)
                : Results.BadRequest(response);
        });

        app.MapPost("/scripts/run-file", async (
            ScriptFileRunRequest request,
            ScriptRunService scriptRunner,
            CancellationToken cancellationToken) =>
        {
            var response = await scriptRunner.RunFileAsync(request, cancellationToken);
            return response.Ok
                ? Results.Ok(response)
                : Results.BadRequest(response);
        });
    }
}

