using Slasher.Automation;
using Slasher.Files;
using Slasher.Windows;

namespace Slasher.Api;

public static partial class SlasherEndpointExtensions
{
    private static void MapAutomationEndpoints(WebApplication app)
    {
        app.MapPost("/automation/runs", (StartAutomationRunRequest request, AutomationRunArtifactStore artifactStore) =>
        {
            var report = artifactStore.StartRun(
                request.Name,
                request.Mode,
                request.EntryPoint,
                request.CapturePolicy,
                string.IsNullOrWhiteSpace(request.Purpose)
                    ? null
                    : new Dictionary<string, object?> { ["purpose"] = request.Purpose });

            return Results.Ok(report);
        });

        app.MapGet("/automation/runs", (int? limit, AutomationRunArtifactStore artifactStore) =>
        {
            return Results.Ok(new AutomationRunListResponse(artifactStore.ListRuns(limit ?? 20)));
        });

        app.MapGet("/automation/runs/{runId}", (string runId, AutomationRunArtifactStore artifactStore) =>
        {
            return artifactStore.TryReadRun(runId, out var report)
                ? Results.Ok(report)
                : Results.NotFound(new ErrorResponse("run_not_found", $"Run '{runId}' was not found."));
        });

        app.MapGet("/automation/runs/{runId}/events", (string runId, AutomationRunArtifactStore artifactStore) =>
        {
            return artifactStore.TryReadEvents(runId, out var events)
                ? Results.Ok(new AutomationRunEventsResponse(runId, events))
                : Results.NotFound(new ErrorResponse("run_events_not_found", $"Events for run '{runId}' were not found."));
        });

        app.MapGet("/automation/runs/{runId}/summary", (string runId, AutomationRunArtifactStore artifactStore) =>
        {
            return artifactStore.TryReadSummary(runId, out var summary)
                ? Results.Text(summary, "text/plain")
                : Results.NotFound(new ErrorResponse("run_summary_not_found", $"Summary for run '{runId}' was not found."));
        });

        app.MapGet("/automation/runs/{runId}/logs/script", (string runId, AutomationRunArtifactStore artifactStore) =>
        {
            return artifactStore.TryReadScriptLog(runId, out var log)
                ? Results.Text(log, "text/plain")
                : Results.NotFound(new ErrorResponse("run_script_log_not_found", $"Script log for run '{runId}' was not found."));
        });

        app.MapGet("/automation/runs/{runId}/report", (string runId, AutomationRunArtifactStore artifactStore) =>
        {
            return artifactStore.TryReadHtmlReport(runId, out var html)
                ? Results.Text(html, "text/html")
                : Results.NotFound(new ErrorResponse("run_report_not_found", $"HTML report for run '{runId}' was not found."));
        });

        app.MapGet("/automation/runs/{runId}/artifacts/raw", (
            string runId,
            string path,
            AutomationRunArtifactStore artifactStore) =>
        {
            return artifactStore.TryResolveArtifactFile(runId, path, out var fullPath, out var mimeType)
                ? Results.File(fullPath, mimeType, enableRangeProcessing: true)
                : Results.NotFound(new ErrorResponse("artifact_not_found", $"Artifact '{path}' for run '{runId}' was not found."));
        });

        app.MapGet("/automation/runs/{runId}/artifacts/content", (
            string runId,
            string path,
            AutomationRunArtifactStore artifactStore) =>
        {
            return artifactStore.TryReadArtifactContent(runId, path, out var content)
                ? Results.Ok(content)
                : Results.NotFound(new ErrorResponse("artifact_not_found", $"Artifact '{path}' for run '{runId}' was not found."));
        });
    }
}

