using Slasher.Api;
using Slasher.Windows;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private sealed record ScriptLine(
        int Sequence,
        int Line,
        string Command,
        string SourceFile,
        string? Function,
        IReadOnlyList<AutomationSourceFrame> Stack);

    private sealed record ScriptBlockMatch(int End, int? ElseIndex = null, IReadOnlyList<int>? MiddleIndexes = null)
    {
        public IReadOnlyList<int> MiddleIndexes { get; init; } = MiddleIndexes ?? [];
    }

    private sealed record ScriptAssignment(IReadOnlyList<string> Tokens, string? VariableName);

    private enum ScriptVariableScope
    {
        Global,
        File,
        Local
    }

    private sealed record ScopedVariableArgs(
        ScriptVariableScope? Scope,
        string Name,
        IReadOnlyList<string> Args);

    private sealed record ScriptCallFrame(
        int Id,
        string Name,
        string SourceFile,
        string LocalKey,
        AutomationSourceFrame Caller,
        string? PreviousStep);

    private sealed record ScriptCommandResult(
        object? Value,
        string? SelectedHandle = null,
        WindowInfo? SelectedWindow = null,
        ScreenshotResponse? Screenshot = null,
        ScreenshotResponse? PreviewScreenshot = null,
        IReadOnlyList<AutomationLogEntry>? Logs = null,
        string? AssignmentName = null,
        object? AssignmentValue = null,
        string? StepName = null,
        IReadOnlyList<AutomationEvidence>? Evidence = null)
    {
        public IReadOnlyList<AutomationLogEntry> Logs { get; init; } = Logs ?? [];
        public IReadOnlyList<AutomationEvidence> Evidence { get; init; } = Evidence ?? [];
    }
}

