namespace Slasher.Automation;
public sealed record AutomationRunReport(
    int SchemaVersion,
    string RunId,
    string Name,
    string Status,
    string Mode,
    string? EntryPoint,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    long? DurationMs,
    string ArtifactRoot,
    int EventCount,
    int? FailedEventSequence,
    AutomationTarget? SelectedTarget,
    AutomationError? Error,
    AutomationRunArtifacts Artifacts,
    CapturePolicy CapturePolicy,
    IReadOnlyDictionary<string, object?>? Metadata = null);

public sealed record AutomationRunArtifacts(
    string Run,
    string Events,
    string Summary,
    string Report,
    string Screenshots,
    string Logs,
    string Attachments,
    string? ScriptLog = null);

