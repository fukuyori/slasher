using Slasher.Api;
using Slasher.Windows;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private async Task RecordScriptErrorAsync(
        ScriptLine line,
        ScriptExecutionState state,
        ScriptCommandException exception,
        bool stopOnError,
        CancellationToken cancellationToken)
    {
        if (state.FinalError is not null && stopOnError)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var eventSequence = state.NextSequence++;
        var startedAt = DateTimeOffset.UtcNow;
        var action = ActionName(line.Command);
        var target = ToTarget(state.SelectedWindow);
        var evidence = new List<AutomationEvidence>();
        var error = EnrichErrorDiagnostics(state, new AutomationError(
            exception.Code,
            exception.Message,
            action,
            ToSource(line, state.CurrentStep, state.CallStack),
            target,
            exception.Recoverable,
            exception.Expected,
            exception.Actual,
            Details: exception.Details));

        if (state.Report.CapturePolicy.CaptureOnError)
        {
            var screenshotHandle = state.Report.CapturePolicy.CaptureTarget.Equals("selected", StringComparison.OrdinalIgnoreCase)
                ? state.SelectedHandle
                : null;
            if (_automation.TakeScreenshot(new ScreenshotRequest(screenshotHandle), out var screenshot, out _) && screenshot is not null)
            {
                SaveScreenshotEvidence(state.Report, eventSequence, "error", screenshot, evidence);
                if ((screenshot.Width > PreviewMaxWidth || screenshot.Height > PreviewMaxHeight)
                    && _automation.TakeScreenshot(
                        new ScreenshotRequest(screenshotHandle, MaxWidth: PreviewMaxWidth, MaxHeight: PreviewMaxHeight),
                        out var preview,
                        out _)
                    && preview is not null)
                {
                    SaveScreenshotEvidence(state.Report, eventSequence, "error-preview", preview, evidence);
                }

                error = error with { Evidence = evidence.ToArray() };
            }
        }

        var endedAt = DateTimeOffset.UtcNow;
        var automationEvent = _artifacts.CreateEvent(
            state.Report,
            eventSequence,
            action,
            startedAt,
            endedAt,
            ok: false,
            step: line.Command,
            source: ToSource(line, state.CurrentStep, state.CallStack),
            target: target,
            parameters: new Dictionary<string, object?> { ["command"] = line.Command },
            logs: [],
            evidence: evidence,
            error: error);

        state.Report = _artifacts.AppendEvent(state.Report, automationEvent);
        state.Events.Add(automationEvent);
        state.FinalError = error;
        await Task.CompletedTask;
    }
}

