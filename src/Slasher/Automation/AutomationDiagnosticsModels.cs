namespace Slasher.Automation;
public sealed record AutomationSource(
    string? File,
    int? Line,
    int? Column,
    string? Command,
    string? Function = null,
    IReadOnlyList<AutomationSourceFrame>? Stack = null);

public sealed record AutomationSourceFrame(
    string? File,
    int? Line,
    int? Column,
    string? Function,
    string? Command);

public sealed record AutomationTarget(
    string Kind,
    string? Handle = null,
    string? Title = null,
    string? ClassName = null,
    int? ProcessId = null,
    string? ProcessName = null,
    AutomationRect? Bounds = null,
    bool? IsVisible = null,
    bool? IsEnabled = null,
    bool? IsMinimized = null,
    string? Scope = null,
    string? Path = null);

public sealed record AutomationRect(int X, int Y, int Width, int Height);

public sealed record AutomationEvidence(
    string Kind,
    string Role,
    string Path,
    string? MimeType = null,
    int? Width = null,
    int? Height = null,
    IReadOnlyDictionary<string, object?>? Metadata = null);

public sealed record AutomationError(
    string Code,
    string Message,
    string? Action = null,
    AutomationSource? Source = null,
    AutomationTarget? Target = null,
    bool Recoverable = true,
    object? Expected = null,
    object? Actual = null,
    IReadOnlyList<AutomationEvidence>? Evidence = null,
    IReadOnlyDictionary<string, object?>? Details = null);

public sealed record AutomationLogEntry(
    DateTimeOffset Timestamp,
    string Level,
    string Source,
    string Message,
    IReadOnlyDictionary<string, object?>? Data = null);

