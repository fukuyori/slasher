using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private async Task<ScriptCommandResult> ExecuteWaitCommandAsync(
        IReadOnlyList<string> args,
        string? selectedHandle,
        CancellationToken cancellationToken)
    {
        if (args.Count >= 1
            && (args[0].Equals("screenStable", StringComparison.OrdinalIgnoreCase)
                || args[0].Equals("screen-stable", StringComparison.OrdinalIgnoreCase)
                || (args.Count >= 2
                    && args[0].Equals("screen", StringComparison.OrdinalIgnoreCase)
                    && args[1].Equals("stable", StringComparison.OrdinalIgnoreCase))))
        {
            return await ExecuteWaitScreenStableAsync(args, selectedHandle, cancellationToken);
        }

        if (args.Count >= 2 && args[0].Equals("window", StringComparison.OrdinalIgnoreCase))
        {
            var timeoutMs = int.TryParse(args[^1], out var parsedTimeout) ? parsedTimeout : 10000;
            var titleEnd = int.TryParse(args[^1], out _) ? args.Count - 1 : args.Count;
            var title = string.Join(' ', args.Skip(1).Take(titleEnd - 1));
            var window = await _automation.WaitForWindowAsync(new WindowQueryRequest(title, TimeoutMs: timeoutMs), cancellationToken);
            if (window is null)
            {
                throw new ScriptCommandException("window_timeout", $"Timed out waiting for window '{title}'.");
            }

            return new ScriptCommandResult(window, window.Handle, window, AssignmentValue: window);
        }

        RequireArgs(args, 1, "wait requires milliseconds or 'wait window <title> [timeoutMs]'.");
        if (!int.TryParse(args[0], out var delayMs) || delayMs < 0)
        {
            throw new ScriptCommandException("invalid_wait", "wait requires a non-negative millisecond value.");
        }

        await Task.Delay(delayMs, cancellationToken);
        return new ScriptCommandResult(new { waitedMs = delayMs });
    }

    private async Task<ScriptCommandResult> ExecuteWaitScreenStableAsync(
        IReadOnlyList<string> args,
        string? selectedHandle,
        CancellationToken cancellationToken)
    {
        var index = args.Count >= 2
            && args[0].Equals("screen", StringComparison.OrdinalIgnoreCase)
            && args[1].Equals("stable", StringComparison.OrdinalIgnoreCase)
            ? 2
            : 1;
        var scope = "selected";
        if (args.Count > index
            && (args[index].Equals("selected", StringComparison.OrdinalIgnoreCase)
                || args[index].Equals("full", StringComparison.OrdinalIgnoreCase)))
        {
            scope = args[index].ToLowerInvariant();
            index++;
        }

        var stableMs = TryReadInt(args, index, 500);
        var timeoutMs = TryReadInt(args, index + 1, 5000);
        if (stableMs < 0 || timeoutMs < 0)
        {
            throw new ScriptCommandException("invalid_wait", "wait screenStable requires non-negative stableMs and timeoutMs.");
        }

        var handle = scope.Equals("selected", StringComparison.OrdinalIgnoreCase) ? selectedHandle : null;
        var startedAt = DateTimeOffset.UtcNow;
        string? lastImage = null;
        DateTimeOffset? unchangedSince = null;
        var samples = 0;

        while ((DateTimeOffset.UtcNow - startedAt).TotalMilliseconds <= timeoutMs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_automation.TakeScreenshot(new ScreenshotRequest(handle, MaxWidth: 320, MaxHeight: 180), out var screenshot, out _) || screenshot is null)
            {
                throw new ScriptCommandException("screen_capture_failed", "Could not capture the screen while waiting for stability.");
            }

            samples++;
            if (lastImage is not null && string.Equals(lastImage, screenshot.Base64Image, StringComparison.Ordinal))
            {
                unchangedSince ??= DateTimeOffset.UtcNow;
                var unchangedMs = (DateTimeOffset.UtcNow - unchangedSince.Value).TotalMilliseconds;
                if (unchangedMs >= stableMs)
                {
                    return new ScriptCommandResult(new
                    {
                        stable = true,
                        stableMs,
                        timeoutMs,
                        scope,
                        samples,
                        waitedMs = (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds
                    });
                }
            }
            else
            {
                lastImage = screenshot.Base64Image;
                unchangedSince = DateTimeOffset.UtcNow;
            }

            await Task.Delay(Math.Min(100, Math.Max(25, stableMs / 5)), cancellationToken);
        }

        throw new ScriptCommandException(
            "screen_stable_timeout",
            $"Timed out waiting for {scope} screen to remain stable for {stableMs} ms.",
            details: new Dictionary<string, object?>
            {
                ["scope"] = scope,
                ["stableMs"] = stableMs,
                ["timeoutMs"] = timeoutMs,
                ["samples"] = samples
            });
    }

    private static int TryReadInt(IReadOnlyList<string> args, int index, int fallback)
    {
        return args.Count > index && int.TryParse(args[index], out var value)
            ? value
            : fallback;
    }
}
