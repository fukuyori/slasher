using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private ScriptCommandResult ExecuteScreenAssert(IReadOnlyList<string> args, string? selectedHandle)
    {
        RequireArgs(args, 2, "assert screen syntax is: assert screen contains <text> [selected|full].");
        if (!args[0].Equals("contains", StringComparison.OrdinalIgnoreCase))
        {
            throw new ScriptCommandException("unsupported_assertion", "assert screen currently supports: contains.");
        }

        var hasExplicitScope = args[^1].Equals("selected", StringComparison.OrdinalIgnoreCase)
            || args[^1].Equals("full", StringComparison.OrdinalIgnoreCase);
        var scope = hasExplicitScope ? args[^1].ToLowerInvariant() : "selected";
        var textEnd = hasExplicitScope ? args.Count - 1 : args.Count;
        var expected = string.Join(' ', args.Skip(1).Take(textEnd - 1));
        if (string.IsNullOrWhiteSpace(expected))
        {
            throw new ScriptCommandException("invalid_assertion", "assert screen contains requires expected text.");
        }

        var handle = scope.Equals("full", StringComparison.OrdinalIgnoreCase) ? null : RequireSelected(selectedHandle);
        if (!_automation.TakeScreenshot(new ScreenshotRequest(handle, MaxWidth: PreviewMaxWidth, MaxHeight: PreviewMaxHeight), out var screenshot, out var error)
            || screenshot is null)
        {
            throw FromError(error, "screen_capture_failed");
        }

        throw new ScriptCommandException(
            "screen_contains_unavailable",
            "assert screen contains needs OCR or image recognition, which is not implemented yet. A failure screenshot was captured for review.",
            details: new Dictionary<string, object?>
            {
                ["scope"] = scope,
                ["previewWidth"] = screenshot.Width,
                ["previewHeight"] = screenshot.Height,
                ["ocrRequired"] = true
            },
            Expected: new { text = expected, scope },
            Actual: new { text = (string?)null, reason = "ocr_unavailable" });
    }
}
