using Slasher.Api;
using Slasher.Windows;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private async Task ExecuteStepAsync(
        ScriptLine line,
        ScriptExecutionState state,
        ScriptRunRequest request,
        CancellationToken cancellationToken)
    {
        var eventSequence = state.NextSequence++;
        var startedAt = DateTimeOffset.UtcNow;
        object? result = null;
        AutomationError? error = null;
        AutomationTarget? target = ToTarget(state.SelectedWindow);
        var evidence = new List<AutomationEvidence>();
        var logs = new List<AutomationLogEntry>();
        ScriptCommandResult? commandResult = null;
        var expandedCommand = line.Command;
        var action = ActionName(line.Command);
        var ok = false;

        if (state.Report.CapturePolicy.CaptureBeforeEachStep)
        {
            CaptureStepEvidenceIfNeeded(state, eventSequence, "before", evidence);
        }

        try
        {
            expandedCommand = ExpandVariables(line.Command, state.ResolveVariables(line));
            action = ActionName(expandedCommand);
            commandResult = await ExecuteCommandAsync(expandedCommand, state.SelectedHandle, state, line, cancellationToken);
            state.SelectedHandle = commandResult.SelectedHandle ?? state.SelectedHandle;
            state.SelectedWindow = commandResult.SelectedWindow ?? state.SelectedWindow;
            result = commandResult.Value;
            target = ToTarget(commandResult.SelectedWindow ?? state.SelectedWindow);
            logs.AddRange(commandResult.Logs);
            evidence.AddRange(commandResult.Evidence);

            if (commandResult.Screenshot is not null)
            {
                SaveScreenshotEvidence(state.Report, eventSequence, "after", commandResult, evidence);
                result = ToScreenshotResult(commandResult);
            }

            ok = true;
            state.AssignVariable(line, "_", result, ScriptVariableScope.Global);
            if (state.SelectedWindow is not null)
            {
                state.AssignVariable(line, "selected", state.SelectedWindow, ScriptVariableScope.Global);
            }

            if (!string.IsNullOrWhiteSpace(commandResult.AssignmentName))
            {
                state.AssignVariable(line, commandResult.AssignmentName, commandResult.AssignmentValue ?? result, ScriptVariableScope.Global);
            }

            if (!string.IsNullOrWhiteSpace(commandResult.StepName))
            {
                state.CurrentStep = commandResult.StepName;
            }

            if (state.Report.CapturePolicy.CaptureAfterEachStep && commandResult.Screenshot is null)
            {
                CaptureStepEvidenceIfNeeded(state, eventSequence, "after", evidence);
            }
        }
        catch (ScriptCommandException ex)
        {
            error = EnrichErrorDiagnostics(state, new AutomationError(
                ex.Code,
                ex.Message,
                action,
                ToSource(line, state.CurrentStep, state.CallStack),
                target,
                ex.Recoverable,
                ex.Expected,
                ex.Actual,
                Details: ex.Details));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            error = EnrichErrorDiagnostics(state, new AutomationError(
                "script_command_failed",
                ex.Message,
                action,
                ToSource(line, state.CurrentStep, state.CallStack),
                target));
        }

        error = CaptureErrorEvidenceIfNeeded(state, eventSequence, evidence, error);

        var endedAt = DateTimeOffset.UtcNow;
        var automationEvent = _artifacts.CreateEvent(
            state.Report,
            eventSequence,
            action,
            startedAt,
            endedAt,
            ok,
            commandResult?.StepName ?? state.CurrentStep ?? line.Command,
            ToSource(line, commandResult?.StepName ?? state.CurrentStep, state.CallStack),
            target,
            new Dictionary<string, object?>
            {
                ["command"] = line.Command,
                ["expandedCommand"] = expandedCommand == line.Command ? null : expandedCommand,
                ["testStep"] = commandResult?.StepName ?? state.CurrentStep
            },
            result,
            logs,
            evidence: evidence,
            error: error);

        state.Report = _artifacts.AppendEvent(state.Report, automationEvent);
        state.Events.Add(automationEvent);

        if (!ok)
        {
            state.FinalError = error;
        }
    }
}

