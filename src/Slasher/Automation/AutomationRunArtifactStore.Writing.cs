using System.Text.Json;

namespace Slasher.Automation;

public sealed partial class AutomationRunArtifactStore
{
    private void WriteReport(AutomationRunReport report)
    {
        var path = Path.Combine(_workspaceRoot, report.Artifacts.Run);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(report, JsonOptions));
    }

    private void AppendSummary(AutomationRunReport report, AutomationEvent automationEvent)
    {
        var status = automationEvent.Ok ? "ok" : "failed";
        var line = $"{automationEvent.Sequence:0000} {status} {automationEvent.Action}";
        if (!string.IsNullOrWhiteSpace(automationEvent.Step))
        {
            line += $" [{automationEvent.Step}]";
        }

        if (automationEvent.Source is not null)
        {
            line += $" @{FormatSource(automationEvent.Source)}";
        }

        if (automationEvent.Error is not null)
        {
            line += $" {automationEvent.Error.Code}: {automationEvent.Error.Message}";
        }

        if (automationEvent.Evidence.Count > 0)
        {
            var evidence = string.Join(", ", automationEvent.Evidence.Select(item => $"{item.Kind}:{item.Role}={item.Path}"));
            line += $" evidence[{evidence}]";
        }

        File.AppendAllText(Path.Combine(_workspaceRoot, report.Artifacts.Summary), line + Environment.NewLine);
    }

    private void AppendLogs(AutomationRunReport report, AutomationEvent automationEvent)
    {
        if (automationEvent.Logs.Count == 0)
        {
            return;
        }

        var logsPath = Path.Combine(_workspaceRoot, report.Artifacts.Logs, "script.log");
        Directory.CreateDirectory(Path.GetDirectoryName(logsPath)!);
        var lines = automationEvent.Logs.Select(log =>
            $"{log.Timestamp:O} seq={automationEvent.Sequence:0000} {log.Level} {log.Source}: {log.Message}");
        File.AppendAllLines(logsPath, lines);
    }

    private static string FormatSource(AutomationSource source)
    {
        var file = string.IsNullOrWhiteSpace(source.File) ? "unknown" : source.File;
        var line = source.Line is null ? string.Empty : $":{source.Line}";
        var function = string.IsNullOrWhiteSpace(source.Function) ? string.Empty : $"#{source.Function}";
        return $"{file}{line}{function}";
    }
}

