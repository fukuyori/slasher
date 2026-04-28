using Slasher.Api;
using Slasher.Windows;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private static string ActionName(string command)
    {
        IReadOnlyList<string> tokens;
        try
        {
            tokens = ParseCommandLine(command);
        }
        catch (ScriptCommandException)
        {
            tokens = [];
        }

        var first = tokens.FirstOrDefault()?.ToLowerInvariant() ?? "script";
        return first switch
        {
            "app" or "application" => "app.select",
            "select" => "window.select",
            "foreground" => "window.foreground",
            "restore" or "maximize" or "minimize" or "hide" or "show" => "window.state",
            "move" => "window.move",
            "text" or "type" => "input.text",
            "keys" or "key" => "input.keys",
            "browser" when tokens.Count >= 2 => $"browser.{tokens[1].ToLowerInvariant()}",
            "browser" => "browser",
            "element" when tokens.Count >= 2 => $"element.{tokens[1].ToLowerInvariant()}",
            "element" => "element",
            "capture" => "screen.capture",
            "assert" when tokens.Count >= 2 => $"assert.{tokens[1].ToLowerInvariant()}",
            "assert" => "assert",
            "agent" when tokens.Count >= 2 => $"agent.{tokens[1].ToLowerInvariant()}",
            "agent" => "agent",
            "fail" => "fail",
            "log" => "log",
            "test" when tokens.Count >= 2 => $"test.{tokens[1].ToLowerInvariant()}",
            "step" or "test" => "test.step",
            "close" => "window.close",
            "wait" when tokens.Count >= 2
                && (tokens[1].Equals("screenStable", StringComparison.OrdinalIgnoreCase)
                    || tokens[1].Equals("screen-stable", StringComparison.OrdinalIgnoreCase)
                    || tokens[1].Equals("screen", StringComparison.OrdinalIgnoreCase)) => "wait.screenStable",
            "wait" => "wait",
            "start" => "app.start",
            _ => $"script.{first}"
        };
    }

    private static AutomationSource ToSource(
        ScriptLine line,
        string? currentFunction = null,
        IReadOnlyList<ScriptCallFrame>? callStack = null)
    {
        var function = string.IsNullOrWhiteSpace(currentFunction)
            ? line.Function
            : currentFunction;
        var stack = line.Stack
            .Concat(callStack?.Select(frame => frame.Caller) ?? [])
            .ToArray();

        return new AutomationSource(
            line.SourceFile,
            line.Line,
            1,
            line.Command,
            function,
            stack.Length == 0 ? null : stack);
    }

    private void SaveScreenshotEvidence(
        AutomationRunReport report,
        int sequence,
        string role,
        ScreenshotResponse screenshot,
        ICollection<AutomationEvidence> evidence)
    {
        evidence.Add(_artifacts.SaveScreenshot(
            report,
            sequence,
            role,
            screenshot.MimeType,
            screenshot.Base64Image,
            screenshot.Width,
            screenshot.Height));
    }

    private void SaveScreenshotEvidence(
        AutomationRunReport report,
        int sequence,
        string role,
        ScriptCommandResult commandResult,
        ICollection<AutomationEvidence> evidence)
    {
        if (commandResult.Screenshot is not null)
        {
            SaveScreenshotEvidence(report, sequence, role, commandResult.Screenshot, evidence);
        }

        if (commandResult.PreviewScreenshot is not null)
        {
            SaveScreenshotEvidence(report, sequence, $"{role}-preview", commandResult.PreviewScreenshot, evidence);
        }
    }

    private static AutomationTarget? ToTarget(WindowInfo? window)
    {
        if (window is null)
        {
            return null;
        }

        return new AutomationTarget(
            "window",
            window.Handle,
            window.Title,
            window.ClassName,
            window.ProcessId,
            window.ProcessName,
            new AutomationRect(window.Bounds.X, window.Bounds.Y, window.Bounds.Width, window.Bounds.Height),
            window.IsVisible,
            window.IsEnabled,
            window.IsMinimized);
    }
}

