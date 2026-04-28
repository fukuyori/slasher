using Slasher.Api;
using Slasher.Windows;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private object ToScreenshotResult(ScriptCommandResult commandResult)
    {
        return new
        {
            commandResult.Screenshot!.MimeType,
            commandResult.Screenshot.Width,
            commandResult.Screenshot.Height,
            Preview = commandResult.PreviewScreenshot is null
                ? null
                : new
                {
                    commandResult.PreviewScreenshot.MimeType,
                    commandResult.PreviewScreenshot.Width,
                    commandResult.PreviewScreenshot.Height
                }
        };
    }

    private AutomationError? CaptureErrorEvidenceIfNeeded(
        ScriptExecutionState state,
        int eventSequence,
        List<AutomationEvidence> evidence,
        AutomationError? error)
    {
        if (error is null || !state.Report.CapturePolicy.CaptureOnError)
        {
            return error;
        }

        var screenshotHandle = state.Report.CapturePolicy.CaptureTarget.Equals("selected", StringComparison.OrdinalIgnoreCase)
            ? state.SelectedHandle
            : null;
        if (!_automation.TakeScreenshot(new ScreenshotRequest(screenshotHandle), out var screenshot, out _) || screenshot is null)
        {
            return error;
        }

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

        return error with { Evidence = evidence.ToArray() };
    }

    private void CaptureStepEvidenceIfNeeded(
        ScriptExecutionState state,
        int eventSequence,
        string role,
        List<AutomationEvidence> evidence)
    {
        var screenshotHandle = state.Report.CapturePolicy.CaptureTarget.Equals("selected", StringComparison.OrdinalIgnoreCase)
            ? state.SelectedHandle
            : null;
        if (!_automation.TakeScreenshot(new ScreenshotRequest(screenshotHandle), out var screenshot, out _) || screenshot is null)
        {
            return;
        }

        SaveScreenshotEvidence(state.Report, eventSequence, role, screenshot, evidence);
        if ((screenshot.Width > PreviewMaxWidth || screenshot.Height > PreviewMaxHeight)
            && _automation.TakeScreenshot(
                new ScreenshotRequest(screenshotHandle, MaxWidth: PreviewMaxWidth, MaxHeight: PreviewMaxHeight),
                out var preview,
                out _)
            && preview is not null)
        {
            SaveScreenshotEvidence(state.Report, eventSequence, $"{role}-preview", preview, evidence);
        }
    }
}
