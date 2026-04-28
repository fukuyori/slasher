using System.Text.Json;

namespace Slasher.Automation;

public sealed partial class AutomationRunArtifactStore
{
    private static string CreateRunId(DateTimeOffset startedAt, string name)
    {
        var slug = Slugify(name);
        var suffix = Random.Shared.Next(0, 0xFFFF).ToString("x4");
        return $"{startedAt:yyyyMMdd-HHmmss}-{slug}-{suffix}";
    }

    private static string Slugify(string value)
    {
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var slug = new string(chars).Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(slug) ? "run" : slug[..Math.Min(slug.Length, 40)];
    }
}

