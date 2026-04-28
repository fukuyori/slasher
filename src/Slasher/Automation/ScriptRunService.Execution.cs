using Slasher.Api;
using Slasher.Windows;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private async Task<ScriptRunResponse> ExecuteRunAsync(
        IReadOnlyList<ScriptLine> lines,
        AutomationRunReport report,
        ScriptRunRequest request,
        CancellationToken cancellationToken)
    {
        var state = new ScriptExecutionState(report);
        try
        {
            await ExecuteBlockAsync(lines, 0, lines.Count, state, request, cancellationToken);
        }
        catch (ScriptCommandException ex)
        {
            await RecordScriptErrorAsync(
                lines.FirstOrDefault() ?? new ScriptLine(1, 1, "script", report.EntryPoint ?? "inline-script", null, []),
                state,
                ex,
                request.StopOnError,
                cancellationToken);
        }

        var status = state.FinalError is null ? AutomationRunStatus.Passed : AutomationRunStatus.Failed;
        state.Report = _artifacts.CompleteRun(state.Report, status, state.FinalError, ToTarget(state.SelectedWindow));
        return new ScriptRunResponse(state.FinalError is null, state.Report, state.Events, state.FinalError);
    }
}

