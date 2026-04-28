using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private static void RequireArgs(IReadOnlyList<string> args, int count, string message)
    {
        if (args.Count < count)
        {
            throw new ScriptCommandException("invalid_arguments", message);
        }
    }

    private static string? JoinArgs(IReadOnlyList<string> args, int start)
    {
        return args.Count <= start ? null : string.Join(' ', args.Skip(start));
    }

    private static string RequireSelected(string? selectedHandle)
    {
        if (string.IsNullOrWhiteSpace(selectedHandle))
        {
            throw new ScriptCommandException("no_selected_window", "No selected window. Use start, select, app select, or foreground first.");
        }

        return selectedHandle;
    }

    private static int ParseInt(string value, string name)
    {
        if (!int.TryParse(value, out var parsed))
        {
            throw new ScriptCommandException("invalid_number", $"{name} must be an integer.");
        }

        return parsed;
    }

    private static ScriptCommandException FromError(ErrorResponse? error, string fallbackCode)
    {
        return new ScriptCommandException(error?.Code ?? fallbackCode, error?.Message ?? fallbackCode);
    }
}

