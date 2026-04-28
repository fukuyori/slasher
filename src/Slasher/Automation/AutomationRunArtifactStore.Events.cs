namespace Slasher.Automation;

public sealed partial class AutomationRunArtifactStore
{
    public AutomationEvent CreateEvent(
        AutomationRunReport report,
        int sequence,
        string action,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        bool ok,
        string? step = null,
        AutomationSource? source = null,
        AutomationTarget? target = null,
        IReadOnlyDictionary<string, object?>? parameters = null,
        object? result = null,
        IReadOnlyList<AutomationLogEntry>? logs = null,
        IReadOnlyList<AutomationEvidence>? evidence = null,
        AutomationError? error = null)
    {
        return new AutomationEvent(
            AutomationSchema.Version,
            report.RunId,
            sequence,
            step,
            action,
            source,
            target,
            parameters ?? new Dictionary<string, object?>(),
            result,
            logs ?? [],
            evidence ?? [],
            error,
            startedAt,
            endedAt,
            (long)(endedAt - startedAt).TotalMilliseconds,
            ok);
    }

    public AutomationEvidence SaveScreenshot(
        AutomationRunReport report,
        int sequence,
        string role,
        string mimeType,
        string base64Image,
        int width,
        int height)
    {
        var extension = mimeType.ToLowerInvariant() switch
        {
            "image/bmp" => "bmp",
            "image/png" => "png",
            "image/jpeg" => "jpg",
            _ => "img"
        };
        var relativePath = Path.Combine(report.Artifacts.Screenshots, $"{sequence:0000}-{role}.{extension}");
        var absolutePath = Path.Combine(_workspaceRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        File.WriteAllBytes(absolutePath, Convert.FromBase64String(base64Image));

        return new AutomationEvidence(
            "screenshot",
            role,
            relativePath,
            mimeType,
            width,
            height);
    }

    public AutomationEvidence SaveAttachment(
        AutomationRunReport report,
        string sourcePath,
        string role,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        var source = Path.GetFullPath(sourcePath);
        var fileName = SanitizeFileName(Path.GetFileName(source));
        var destinationName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}-{fileName}";
        var relativePath = Path.Combine(report.Artifacts.Attachments, destinationName);
        var absolutePath = Path.Combine(_workspaceRoot, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        File.Copy(source, absolutePath, overwrite: false);

        var fileInfo = new FileInfo(source);
        var nextMetadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["originalPath"] = source,
            ["originalName"] = Path.GetFileName(source),
            ["size"] = fileInfo.Length
        };

        if (metadata is not null)
        {
            foreach (var item in metadata)
            {
                nextMetadata[item.Key] = item.Value;
            }
        }

        return new AutomationEvidence(
            "attachment",
            string.IsNullOrWhiteSpace(role) ? "attachment" : role,
            relativePath,
            GuessMimeType(source),
            Metadata: nextMetadata);
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "attachment";
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value;
    }
}

