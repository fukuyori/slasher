namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private async Task<ScriptCommandResult> ExecuteAssertAsync(
        IReadOnlyList<string> args,
        string? selectedHandle,
        Dictionary<string, object?> variables,
        ScriptLine line,
        CancellationToken cancellationToken)
    {
        RequireArgs(args, 1, "assert requires a condition.");
        var subject = args[0].ToLowerInvariant();

        return subject switch
        {
            "window" => await ExecuteWindowAssertAsync(args.Skip(1).ToArray(), cancellationToken),
            "value" => ExecuteValueAssert(args.Skip(1).ToArray()),
            "variable" or "var" => ExecuteVariableAssert(args.Skip(1).ToArray(), variables),
            "screen" => ExecuteScreenAssert(args.Skip(1).ToArray(), selectedHandle),
            "image" => ExecuteImageAssert(args.Skip(1).ToArray(), selectedHandle, line),
            "element" => ExecuteElementAssert(args.Skip(1).ToArray(), selectedHandle),
            "selected" or "foreground" or "active" or "title" => ExecuteWindowTitleAssert(subject, args, selectedHandle),
            _ => throw new ScriptCommandException(
                "unsupported_assertion",
                "assert supports value, variable exists, selected title, foreground title, title, window exists, screen contains, image match, and element exists/text.")
        };
    }
}
