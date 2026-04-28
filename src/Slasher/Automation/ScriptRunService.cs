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

        return await ExecuteRunAsync(lines, runReport, new ScriptRunRequest(string.Empty, name, request.StopOnError, request.CapturePolicy), cancellationToken);
    }

    public async Task<ScriptRunResponse> RunAsync(ScriptRunRequest request, CancellationToken cancellationToken)
    {
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
}

