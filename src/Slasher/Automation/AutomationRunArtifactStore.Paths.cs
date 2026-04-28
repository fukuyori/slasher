using System.Text.Json;

namespace Slasher.Automation;

public sealed partial class AutomationRunArtifactStore
{
    private bool TryResolveRunRoot(string runId, out string runRoot)
    {
        runRoot = string.Empty;
        if (string.IsNullOrWhiteSpace(runId)
            || runId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || runId.Contains(Path.DirectorySeparatorChar)
            || runId.Contains(Path.AltDirectorySeparatorChar))
        {
            return false;
        }

        runRoot = Path.GetFullPath(Path.Combine(_workspaceRoot, "artifacts", "runs", runId));
        var runsRoot = Path.GetFullPath(Path.Combine(_workspaceRoot, "artifacts", "runs"));
        return runRoot.StartsWith(runsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private bool TryResolveArtifactPath(
        string runId,
        string relativePath,
        out string fullPath,
        out string normalizedRelativePath)
    {
        fullPath = string.Empty;
        normalizedRelativePath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            return false;
        }

        if (!TryResolveRunRoot(runId, out var runRoot))
        {
            return false;
        }

        var runRelativePrefix = Path.Combine("artifacts", "runs", runId) + Path.DirectorySeparatorChar;
        var normalizedInput = relativePath
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

        fullPath = normalizedInput.StartsWith(runRelativePrefix, StringComparison.OrdinalIgnoreCase)
            ? Path.GetFullPath(Path.Combine(_workspaceRoot, normalizedInput))
            : Path.GetFullPath(Path.Combine(runRoot, normalizedInput));

        if (!fullPath.StartsWith(runRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        normalizedRelativePath = Path.GetRelativePath(_workspaceRoot, fullPath);
        return true;
    }

    private static string GuessMimeType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".bmp" => "image/bmp",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".json" or ".jsonl" => "application/json",
            ".txt" or ".log" => "text/plain",
            _ => "application/octet-stream"
        };
    }
}

