using System.Text.Json;

namespace Slasher.Automation;

public sealed partial class AutomationRunArtifactStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions JsonLineOptions = new(JsonSerializerDefaults.Web);

    private readonly string _workspaceRoot;

    public AutomationRunArtifactStore(IHostEnvironment environment)
    {
        _workspaceRoot = environment.ContentRootPath;
    }

    public AutomationRunReport StartRun(
        string name,
        string mode,
        string? entryPoint = null,
        CapturePolicy? capturePolicy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var runId = CreateRunId(startedAt, name);
        var artifactRoot = Path.Combine("artifacts", "runs", runId);
        var screenshots = Path.Combine(artifactRoot, "screenshots");
        var logs = Path.Combine(artifactRoot, "logs");
        var attachments = Path.Combine(artifactRoot, "attachments");

        Directory.CreateDirectory(Path.Combine(_workspaceRoot, screenshots));
        Directory.CreateDirectory(Path.Combine(_workspaceRoot, logs));
        Directory.CreateDirectory(Path.Combine(_workspaceRoot, attachments));

        var artifacts = new AutomationRunArtifacts(
            Run: Path.Combine(artifactRoot, "run.json"),
            Events: Path.Combine(artifactRoot, "events.jsonl"),
            Summary: Path.Combine(artifactRoot, "summary.txt"),
            Report: Path.Combine(artifactRoot, "report.html"),
            Screenshots: screenshots,
            Logs: logs,
            Attachments: attachments,
            ScriptLog: Path.Combine(logs, "script.log"));

        var report = new AutomationRunReport(
            AutomationSchema.Version,
            runId,
            name,
            AutomationRunStatus.Running,
            mode,
            entryPoint,
            startedAt,
            null,
            null,
            artifactRoot,
            0,
            null,
            null,
            null,
            artifacts,
            capturePolicy ?? new CapturePolicy(),
            metadata);

        WriteReport(report);
        File.WriteAllText(Path.Combine(_workspaceRoot, artifacts.Events), string.Empty);
        File.WriteAllText(Path.Combine(_workspaceRoot, artifacts.Summary), $"Run {runId} started: {name}{Environment.NewLine}");

        return report;
    }

    public AutomationRunReport AppendEvent(AutomationRunReport report, AutomationEvent automationEvent)
    {
        var eventsPath = Path.Combine(_workspaceRoot, report.Artifacts.Events);
        File.AppendAllText(eventsPath, JsonSerializer.Serialize(automationEvent, JsonLineOptions) + Environment.NewLine);

        var nextReport = report with
        {
            EventCount = Math.Max(report.EventCount, automationEvent.Sequence),
            FailedEventSequence = !automationEvent.Ok && report.FailedEventSequence is null
                ? automationEvent.Sequence
                : report.FailedEventSequence,
            SelectedTarget = automationEvent.Target ?? report.SelectedTarget,
            Error = automationEvent.Error ?? report.Error
        };

        WriteReport(nextReport);
        AppendSummary(nextReport, automationEvent);
        AppendLogs(nextReport, automationEvent);
        return nextReport;
    }

    public AutomationRunReport CompleteRun(
        AutomationRunReport report,
        string status,
        AutomationError? error = null,
        AutomationTarget? selectedTarget = null)
    {
        var endedAt = DateTimeOffset.UtcNow;
        var nextReport = report with
        {
            Status = status,
            EndedAt = endedAt,
            DurationMs = (long)(endedAt - report.StartedAt).TotalMilliseconds,
            Error = status.Equals(AutomationRunStatus.Passed, StringComparison.OrdinalIgnoreCase)
                ? error
                : error ?? report.Error,
            SelectedTarget = selectedTarget ?? report.SelectedTarget
        };

        WriteReport(nextReport);
        File.AppendAllText(
            Path.Combine(_workspaceRoot, nextReport.Artifacts.Summary),
            $"Run {nextReport.RunId} ended: {nextReport.Status} ({nextReport.DurationMs} ms){Environment.NewLine}");
        WriteHtmlReport(nextReport);
        return nextReport;
    }
}

