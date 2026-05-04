using Slasher.Api;
using Slasher.Files;
using Slasher.Windows;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private const int PreviewMaxWidth = 1280;
    private const int PreviewMaxHeight = 720;
    private const int MaxIncludeDepth = 16;

    private readonly WindowsAutomationService _automation;
    private readonly BrowserAutomationService _browser;
    private readonly FileSystemAutomationService _files;
    private readonly ClipboardService _clipboard;
    private readonly AutomationRunArtifactStore _artifacts;
    private readonly NumadoraPolicyEvaluator _numadoraPolicy = new();
    private readonly string _workspaceRoot;

    public ScriptRunService(
        WindowsAutomationService automation,
        BrowserAutomationService browser,
        FileSystemAutomationService files,
        ClipboardService clipboard,
        AutomationRunArtifactStore artifacts,
        IHostEnvironment environment)
    {
        _automation = automation;
        _browser = browser;
        _files = files;
        _clipboard = clipboard;
        _artifacts = artifacts;
        _workspaceRoot = environment.ContentRootPath;
    }

    public async Task<ScriptRunResponse> RunFileAsync(ScriptFileRunRequest request, CancellationToken cancellationToken)
    {
        string scriptPath;
        string sourceFile;
        string name;
        try
        {
            scriptPath = ResolveScriptPath(request.Path);
            sourceFile = Path.GetRelativePath(_workspaceRoot, scriptPath);
            name = string.IsNullOrWhiteSpace(request.Name)
                ? Path.GetFileNameWithoutExtension(scriptPath)
                : request.Name;
        }
        catch (ScriptCommandException ex)
        {
            var report = _artifacts.StartRun(
                string.IsNullOrWhiteSpace(request.Name) ? "script-file-run" : request.Name,
                AutomationRunMode.Script,
                request.Path,
                request.CapturePolicy);
            var state = new ScriptExecutionState(report);
            await RecordScriptErrorAsync(
                new ScriptLine(1, 1, $"run-file {request.Path}", request.Path, null, []),
                state,
                ex,
                request.StopOnError,
                cancellationToken);
            state.Report = _artifacts.CompleteRun(state.Report, AutomationRunStatus.Failed, state.FinalError, null);
            return new ScriptRunResponse(false, state.Report, state.Events, state.FinalError);
        }

        if (IsRemovedSlasherScript(request.Language, request.Path))
        {
            var report = _artifacts.StartRun(
                name,
                AutomationRunMode.Script,
                sourceFile,
                request.CapturePolicy);
            return await FailRemovedSlasherRunAsync(report, request.StopOnError, cancellationToken);
        }

        if (IsNumadoraRun(request.Language, request.Path))
        {
            return await RunNumadoraPreflightAsync(
                new ScriptCheckRequest(Path: request.Path, Language: "numadora"),
                name,
                sourceFile,
                request.StopOnError,
                request.CapturePolicy,
                request.Purpose,
                request.AllowInteractiveInput,
                cancellationToken);
        }

        var runReport = _artifacts.StartRun(
            name,
            AutomationRunMode.Script,
            sourceFile,
            request.CapturePolicy);

        IReadOnlyList<ScriptLine> lines;
        try
        {
            lines = await ParseScriptFileAsync(scriptPath, cancellationToken);
        }
        catch (ScriptCommandException ex)
        {
            var state = new ScriptExecutionState(runReport);
            await RecordScriptErrorAsync(
                new ScriptLine(1, 1, $"parse {sourceFile}", sourceFile, null, []),
                state,
                ex,
                request.StopOnError,
                cancellationToken);
            state.Report = _artifacts.CompleteRun(state.Report, AutomationRunStatus.Failed, state.FinalError, null);
            return new ScriptRunResponse(false, state.Report, state.Events, state.FinalError);
        }

        return await ExecuteRunAsync(lines, runReport, new ScriptRunRequest(string.Empty, name, request.StopOnError, request.CapturePolicy, Purpose: request.Purpose), cancellationToken);
    }

    public async Task<ScriptRunResponse> RunAsync(ScriptRunRequest request, CancellationToken cancellationToken)
    {
        if (IsRemovedSlasherScript(request.Language, null))
        {
            var removedReport = _artifacts.StartRun(
                string.IsNullOrWhiteSpace(request.Name) ? "script-run" : request.Name,
                AutomationRunMode.Script,
                "POST /scripts/run",
                request.CapturePolicy);
            return await FailRemovedSlasherRunAsync(removedReport, request.StopOnError, cancellationToken);
        }

        if (IsNumadoraRun(request.Language, null))
        {
            return await RunNumadoraPreflightAsync(
                new ScriptCheckRequest(Script: request.Script, Language: "numadora"),
                string.IsNullOrWhiteSpace(request.Name) ? "numadora-script-run" : request.Name,
                "POST /scripts/run",
                request.StopOnError,
                request.CapturePolicy,
                request.Purpose,
                request.AllowInteractiveInput,
                cancellationToken);
        }

        var report = _artifacts.StartRun(
            string.IsNullOrWhiteSpace(request.Name) ? "script-run" : request.Name,
            AutomationRunMode.Script,
            "POST /scripts/run",
            request.CapturePolicy);

        IReadOnlyList<ScriptLine> lines;
        try
        {
            lines = ParseScript(
                request.Script,
                "inline-script",
                _workspaceRoot,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                [],
                inheritedFunction: null,
                depth: 0);
        }
        catch (ScriptCommandException ex)
        {
            var state = new ScriptExecutionState(report);
            await RecordScriptErrorAsync(
                new ScriptLine(1, 1, "parse inline script", "inline-script", null, []),
                state,
                ex,
                request.StopOnError,
                cancellationToken);
            state.Report = _artifacts.CompleteRun(state.Report, AutomationRunStatus.Failed, state.FinalError, null);
            return new ScriptRunResponse(false, state.Report, state.Events, state.FinalError);
        }

        return await ExecuteRunAsync(lines, report, request, cancellationToken);
    }

    private static bool IsNumadoraRun(string? language, string? path)
    {
        if (string.IsNullOrWhiteSpace(language)
            || language.Equals("numadora", StringComparison.OrdinalIgnoreCase)
            || language.Equals("numa", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(path)
            && Path.GetExtension(path).Equals(".numa", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRemovedSlasherScript(string? language, string? path)
    {
        if (language?.Equals("slasher", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(path)
            && Path.GetExtension(path).Equals(".slasher", StringComparison.OrdinalIgnoreCase);
    }

    private static ScriptDiagnostic RemovedSlasherDiagnostic()
    {
        return new ScriptDiagnostic(
            "slasher_language_removed",
            "The legacy Slasher script language has been removed. Use Numadora (.numa) scripts.");
    }

    private async Task<ScriptRunResponse> FailRemovedSlasherRunAsync(
        AutomationRunReport report,
        bool stopOnError,
        CancellationToken cancellationToken)
    {
        var state = new ScriptExecutionState(report);
        await RecordScriptErrorAsync(
            new ScriptLine(1, 1, "legacy slasher script", report.EntryPoint ?? "legacy slasher script", null, []),
            state,
            new ScriptCommandException(
                "slasher_language_removed",
                "The legacy Slasher script language has been removed. Use Numadora (.numa) scripts.",
                Recoverable: false),
            stopOnError,
            cancellationToken);
        state.Report = _artifacts.CompleteRun(state.Report, AutomationRunStatus.Failed, state.FinalError, null);
        return new ScriptRunResponse(false, state.Report, state.Events, state.FinalError);
    }
}

