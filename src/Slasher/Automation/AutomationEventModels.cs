namespace Slasher.Automation;
public sealed record AutomationEvent(
    int SchemaVersion,
    string RunId,
    int Sequence,
    string? Step,
    string Action,
    AutomationSource? Source,
    AutomationTarget? Target,
    IReadOnlyDictionary<string, object?> Parameters,
    object? Result,
    IReadOnlyList<AutomationLogEntry> Logs,
    IReadOnlyList<AutomationEvidence> Evidence,
    AutomationError? Error,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    long DurationMs,
    bool Ok);

