using System.Net;
using System.Text;
using System.Text.Json;

namespace Slasher.Automation;

public sealed partial class AutomationRunArtifactStore
{
    private void WriteHtmlReport(AutomationRunReport report)
    {
        TryReadEvents(report.RunId, out var events);
        var path = Path.Combine(_workspaceRoot, report.Artifacts.Report);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, BuildHtmlReport(report, events), Encoding.UTF8);
    }

    private static string BuildHtmlReport(AutomationRunReport report, IReadOnlyList<AutomationEvent> events)
    {
        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.AppendLine($"<title>Slasher Run {Html(report.RunId)}</title>");
        html.AppendLine("<style>");
        html.AppendLine("body{font-family:Segoe UI,Arial,sans-serif;margin:0;background:#f4f7f9;color:#16202a}");
        html.AppendLine("main{max-width:1180px;margin:0 auto;padding:24px}");
        html.AppendLine("h1{font-size:24px;margin:0 0 8px}");
        html.AppendLine("h2{font-size:16px;margin:24px 0 8px}");
        html.AppendLine(".summary,.event{background:#fff;border:1px solid #d8e1e8;border-radius:6px;padding:14px;margin:10px 0}");
        html.AppendLine(".grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:8px}");
        html.AppendLine(".key{font-size:12px;color:#516170}.value{font-weight:600;word-break:break-word}");
        html.AppendLine(".passed{color:#0a7a4f}.failed{color:#b42318}.running{color:#8a5a00}");
        html.AppendLine(".title{display:flex;gap:8px;align-items:center;flex-wrap:wrap}");
        html.AppendLine(".pill{border:1px solid #cdd8df;border-radius:999px;padding:2px 8px;font-size:12px;background:#f7fafc}");
        html.AppendLine(".error{background:#fff1f0;border-left:4px solid #b42318;padding:10px;margin-top:10px}");
        html.AppendLine(".logs{background:#0f1720;color:#d7e1ea;border-radius:4px;padding:10px;white-space:pre-wrap;overflow:auto}");
        html.AppendLine(".evidence{display:flex;gap:8px;flex-wrap:wrap;margin-top:8px}");
        html.AppendLine(".shot{border:1px solid #cdd8df;border-radius:4px;display:block;max-height:260px;max-width:100%;object-fit:contain}");
        html.AppendLine(".shot-card{display:grid;gap:4px;max-width:360px}");
        html.AppendLine("a{color:#005ea8;text-decoration:none}a:hover{text-decoration:underline}");
        html.AppendLine("details{margin-top:8px}pre{background:#f7fafc;border:1px solid #d8e1e8;border-radius:4px;padding:8px;white-space:pre-wrap;overflow:auto}");
        html.AppendLine("</style>");
        html.AppendLine("</head><body><main>");
        html.AppendLine($"<h1>Slasher Run <span class=\"{Html(report.Status)}\">{Html(report.Status)}</span></h1>");
        html.AppendLine("<section class=\"summary\"><div class=\"grid\">");
        AppendField(html, "Run ID", report.RunId);
        AppendField(html, "Name", report.Name);
        AppendField(html, "Mode", report.Mode);
        AppendField(html, "Entry", report.EntryPoint ?? "-");
        AppendField(html, "Started", report.StartedAt.ToString("O"));
        AppendField(html, "Duration", report.DurationMs is null ? "-" : $"{report.DurationMs} ms");
        AppendField(html, "Events", report.EventCount.ToString());
        AppendField(html, "Artifact Root", report.ArtifactRoot);
        html.AppendLine("</div>");
        html.AppendLine("<div class=\"evidence\">");
        AppendArtifactLink(html, report.RunId, "run.json", "run.json");
        AppendArtifactLink(html, report.RunId, "events.jsonl", "events.jsonl");
        AppendArtifactLink(html, report.RunId, "summary.txt", "summary.txt");
        if (events.Any(item => item.Logs.Count > 0))
        {
            AppendArtifactLink(html, report.RunId, "script.log", "logs/script.log");
        }
        html.AppendLine("</div>");
        if (report.Error is not null)
        {
            AppendError(html, report.RunId, report.Error);
        }

        html.AppendLine("</section>");

        html.AppendLine("<h2>Events</h2>");
        foreach (var item in events)
        {
            html.AppendLine($"<section class=\"event\" id=\"event-{item.Sequence}\">");
            html.AppendLine("<div class=\"title\">");
            html.AppendLine($"<strong>#{item.Sequence:0000}</strong>");
            html.AppendLine($"<span class=\"pill {(item.Ok ? "passed" : "failed")}\">{(item.Ok ? "ok" : "failed")}</span>");
            html.AppendLine($"<span class=\"pill\">{Html(item.Action)}</span>");
            if (!string.IsNullOrWhiteSpace(item.Step))
            {
                html.AppendLine($"<span>{Html(item.Step)}</span>");
            }

            html.AppendLine("</div>");
            var sourceText = item.Source is null ? "unknown" : FormatSource(item.Source);
            html.AppendLine($"<div class=\"key\">{Html(sourceText)} | {item.DurationMs} ms</div>");
            if (item.Target is not null)
            {
                html.AppendLine($"<div class=\"key\">target: {Html(item.Target.Title ?? item.Target.Handle ?? item.Target.Kind)}</div>");
            }

            if (item.Logs.Count > 0)
            {
                html.AppendLine("<div class=\"logs\">");
                foreach (var log in item.Logs)
                {
                    html.AppendLine(Html($"{log.Timestamp:O} {log.Level} {log.Source}: {log.Message}"));
                }

                html.AppendLine("</div>");
            }

            if (item.Evidence.Count > 0)
            {
                html.AppendLine("<div class=\"evidence\">");
                foreach (var evidence in item.Evidence)
                {
                    AppendEvidence(html, report.RunId, evidence);
                }

                html.AppendLine("</div>");
            }

            if (item.Error is not null)
            {
                AppendError(html, report.RunId, item.Error);
            }

            html.AppendLine("</section>");
        }

        html.AppendLine("</main></body></html>");
        return html.ToString();
    }

    private static void AppendField(StringBuilder html, string key, string value)
    {
        html.AppendLine("<div>");
        html.AppendLine($"<div class=\"key\">{Html(key)}</div>");
        html.AppendLine($"<div class=\"value\">{Html(value)}</div>");
        html.AppendLine("</div>");
    }

    private static void AppendArtifactLink(StringBuilder html, string runId, string label, string path)
    {
        html.AppendLine($"<a class=\"pill\" href=\"{Html(ToRawArtifactHref(runId, path))}\">{Html(label)}</a>");
    }

    private static void AppendEvidence(StringBuilder html, string runId, AutomationEvidence evidence)
    {
        var href = ToRawArtifactHref(runId, evidence.Path);
        if (evidence.Kind.Equals("screenshot", StringComparison.OrdinalIgnoreCase)
            && IsPreviewEvidence(evidence))
        {
            html.AppendLine("<div class=\"shot-card\">");
            html.AppendLine($"<a href=\"{Html(href)}\"><img class=\"shot\" src=\"{Html(href)}\" alt=\"{Html(evidence.Role)} screenshot\"></a>");
            html.AppendLine($"<a class=\"pill\" href=\"{Html(href)}\">{Html(evidence.Kind)}:{Html(evidence.Role)}</a>");
            html.AppendLine("</div>");
            return;
        }

        html.AppendLine($"<a class=\"pill\" href=\"{Html(href)}\">{Html(evidence.Kind)}:{Html(evidence.Role)}</a>");
    }

    private static void AppendError(StringBuilder html, string runId, AutomationError error)
    {
        html.AppendLine("<div class=\"error\">");
        html.AppendLine($"<strong>{Html(error.Code)}</strong>: {Html(error.Message)}");
        if (error.Source is not null)
        {
            html.AppendLine($"<div class=\"key\">source: {Html(FormatSource(error.Source))}</div>");
        }

        if (error.Evidence is not null && error.Evidence.Count > 0)
        {
            html.AppendLine("<div class=\"evidence\">");
            foreach (var evidence in error.Evidence)
            {
                AppendEvidence(html, runId, evidence);
            }

            html.AppendLine("</div>");
        }

        if (error.Details is not null)
        {
            html.AppendLine("<details open>");
            html.AppendLine("<summary>Diagnostics</summary>");
            html.AppendLine($"<pre>{Html(JsonSerializer.Serialize(error.Details, JsonOptions))}</pre>");
            html.AppendLine("</details>");
        }

        html.AppendLine("</div>");
    }

    private static string Html(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

    private static string ToRawArtifactHref(string runId, string path)
    {
        var normalized = ToRunRelativeArtifactPath(runId, path);
        return $"/automation/runs/{Uri.EscapeDataString(runId)}/artifacts/raw?path={Uri.EscapeDataString(normalized)}";
    }

    private static string ToRunRelativeArtifactPath(string runId, string path)
    {
        var normalized = path.Replace('\\', '/');
        var marker = "/artifacts/runs/";
        var index = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            var afterRuns = normalized[(index + marker.Length)..];
            var firstSlash = afterRuns.IndexOf('/');
            return firstSlash >= 0 ? afterRuns[(firstSlash + 1)..] : afterRuns;
        }

        var artifactsPrefix = "artifacts/runs/";
        if (normalized.StartsWith(artifactsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var afterRuns = normalized[artifactsPrefix.Length..];
            var firstSlash = afterRuns.IndexOf('/');
            return firstSlash >= 0 ? afterRuns[(firstSlash + 1)..] : afterRuns;
        }

        var runPrefix = $"{runId}/";
        if (normalized.StartsWith(runPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return normalized[runPrefix.Length..];
        }

        return normalized;
    }

    private static bool IsPreviewEvidence(AutomationEvidence evidence)
    {
        return evidence.Role.EndsWith("-preview", StringComparison.OrdinalIgnoreCase)
            || evidence.Width is > 0 and <= 1280 && evidence.Height is > 0 and <= 720;
    }
}
