using System.Text.Json;

namespace Slasher.Automation;

public sealed partial class AutomationRunArtifactStore
{
    public IReadOnlyList<AutomationRunReport> ListRuns(int limit = 20)
    {
        var runsRoot = Path.Combine(_workspaceRoot, "artifacts", "runs");
        if (!Directory.Exists(runsRoot))
        {
            return [];
        }

        var safeLimit = Math.Clamp(limit, 1, 200);
        return Directory.EnumerateDirectories(runsRoot)
            .Select(directory => new
            {
                Directory = directory,
                LastWriteTimeUtc = Directory.GetLastWriteTimeUtc(directory)
            })
            .OrderByDescending(item => item.LastWriteTimeUtc)
            .Select(item => TryReadRun(Path.GetFileName(item.Directory), out var report) ? report : null)
            .Where(report => report is not null)
            .Take(safeLimit)
            .Select(report => report!)
            .ToArray();
    }

    public bool TryReadRun(string runId, out AutomationRunReport? report)
    {
        report = null;
        if (!TryResolveRunRoot(runId, out var runRoot))
        {
            return false;
        }

        var path = Path.Combine(runRoot, "run.json");
        if (!File.Exists(path))
        {
            return false;
        }

        report = JsonSerializer.Deserialize<AutomationRunReport>(File.ReadAllText(path), JsonOptions);
        return report is not null;
    }

    public bool TryReadEvents(string runId, out IReadOnlyList<AutomationEvent> events)
    {
        events = [];
        if (!TryResolveRunRoot(runId, out var runRoot))
        {
            return false;
        }

        var path = Path.Combine(runRoot, "events.jsonl");
        if (!File.Exists(path))
        {
            return false;
        }

        var parsed = new List<AutomationEvent>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var automationEvent = JsonSerializer.Deserialize<AutomationEvent>(line, JsonLineOptions);
            if (automationEvent is not null)
            {
                parsed.Add(automationEvent);
            }
        }

        events = parsed;
        return true;
    }

    public bool TryReadSummary(string runId, out string summary)
    {
        summary = string.Empty;
        if (!TryResolveRunRoot(runId, out var runRoot))
        {
            return false;
        }

        var path = Path.Combine(runRoot, "summary.txt");
        if (!File.Exists(path))
        {
            return false;
        }

        summary = File.ReadAllText(path);
        return true;
    }

    public bool TryReadHtmlReport(string runId, out string html)
    {
        html = string.Empty;
        if (!TryResolveRunRoot(runId, out var runRoot))
        {
            return false;
        }

        var path = Path.Combine(runRoot, "report.html");
        if (!File.Exists(path))
        {
            return false;
        }

        html = File.ReadAllText(path);
        return true;
    }

    public bool TryReadScriptLog(string runId, out string log)
    {
        log = string.Empty;
        if (!TryResolveArtifactPath(runId, Path.Combine("logs", "script.log"), out var fullPath, out _))
        {
            return false;
        }

        if (!File.Exists(fullPath))
        {
            return false;
        }

        log = File.ReadAllText(fullPath);
        return true;
    }

    public bool TryResolveArtifactFile(string runId, string relativePath, out string fullPath, out string mimeType)
    {
        fullPath = string.Empty;
        mimeType = string.Empty;
        if (!TryResolveArtifactPath(runId, relativePath, out var resolvedPath, out _))
        {
            return false;
        }

        if (!File.Exists(resolvedPath))
        {
            return false;
        }

        fullPath = resolvedPath;
        mimeType = GuessMimeType(resolvedPath);
        return true;
    }

    public bool TryReadArtifactContent(string runId, string relativePath, out AutomationArtifactContent? content)
    {
        content = null;
        if (!TryResolveArtifactPath(runId, relativePath, out var fullPath, out var normalizedRelativePath))
        {
            return false;
        }

        if (!File.Exists(fullPath))
        {
            return false;
        }

        var bytes = File.ReadAllBytes(fullPath);
        content = new AutomationArtifactContent(
            normalizedRelativePath,
            GuessMimeType(fullPath),
            Convert.ToBase64String(bytes),
            bytes.LongLength);
        return true;
    }
}

