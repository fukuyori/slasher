namespace Slasher.Automation;

public static class AutomationSchema
{
    public const int Version = 1;
}

public static class AutomationRunStatus
{
    public const string Running = "running";
    public const string Passed = "passed";
    public const string Failed = "failed";
    public const string Stopped = "stopped";
    public const string Cancelled = "cancelled";
    public const string TimedOut = "timed_out";
}

public static class AutomationRunMode
{
    public const string Http = "http";
    public const string Web = "web";
    public const string Mcp = "mcp";
    public const string Cli = "cli";
    public const string Script = "script";
    public const string Compiled = "compiled";
}

public sealed record StartAutomationRunRequest(
    string Name,
    string Mode = AutomationRunMode.Http,
    string? EntryPoint = null,
    CapturePolicy? CapturePolicy = null);

public sealed record ScriptRunRequest(
    string Script,
    string? Name = null,
    bool StopOnError = true,
    CapturePolicy? CapturePolicy = null);

public sealed record ScriptFileRunRequest(
    string Path,
    string? Name = null,
    bool StopOnError = true,
    CapturePolicy? CapturePolicy = null);

public sealed record ScriptCheckRequest(
    string? Script = null,
    string? Path = null);

public sealed record ScriptCheckResponse(
    bool Ok,
    IReadOnlyList<ScriptDiagnostic> Diagnostics,
    IReadOnlyList<ScriptCheckLine> Lines);

public sealed record ScriptDiagnostic(
    string Code,
    string Message,
    string? File = null,
    int? Line = null,
    int? Column = null,
    string? Command = null,
    string? Function = null,
    IReadOnlyList<AutomationSourceFrame>? Stack = null);

public sealed record ScriptCheckLine(
    int Sequence,
    int Line,
    string Command,
    string SourceFile,
    string? Function);

public sealed record ScriptRunResponse(
    bool Ok,
    AutomationRunReport Run,
    IReadOnlyList<AutomationEvent> Events,
    AutomationError? Error);

public sealed record AutomationRunEventsResponse(
    string RunId,
    IReadOnlyList<AutomationEvent> Events);

public sealed record AutomationRunListResponse(
    IReadOnlyList<AutomationRunReport> Runs);

public sealed record AutomationArtifactContent(
    string Path,
    string MimeType,
    string Base64Content,
    long Length);

