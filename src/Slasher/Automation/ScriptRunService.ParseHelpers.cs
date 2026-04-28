using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private static string? TryGetStepName(IReadOnlyList<string> tokens)
    {
        if (tokens.Count >= 2 && tokens[0].Equals("step", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join(' ', tokens.Skip(1));
        }

        if (tokens.Count >= 3
            && tokens[0].Equals("test", StringComparison.OrdinalIgnoreCase)
            && tokens[1].Equals("step", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join(' ', tokens.Skip(2));
        }

        return null;
    }

    private static string? EmptyToNull(string value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }
}

