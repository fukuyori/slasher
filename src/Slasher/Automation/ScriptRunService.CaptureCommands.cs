using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private ScriptCommandResult ExecuteCaptureCommand(IReadOnlyList<string> args, string? selectedHandle)
    {
        var scope = args.FirstOrDefault() ?? "selected";
        var handle = scope.Equals("full", StringComparison.OrdinalIgnoreCase)
            ? null
            : RequireSelected(selectedHandle);
        if (!_automation.TakeScreenshot(new ScreenshotRequest(handle), out var screenshot, out var error) || screenshot is null)
        {
            throw FromError(error, "capture_failed");
        }

        ScreenshotResponse? preview = null;
        if (screenshot.Width > PreviewMaxWidth || screenshot.Height > PreviewMaxHeight)
        {
            _automation.TakeScreenshot(
                new ScreenshotRequest(handle, MaxWidth: PreviewMaxWidth, MaxHeight: PreviewMaxHeight),
                out preview,
                out _);
        }

        return new ScriptCommandResult(null, Screenshot: screenshot, PreviewScreenshot: preview);
    }
}
