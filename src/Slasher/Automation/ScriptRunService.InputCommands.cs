using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private ScriptCommandResult ExecuteInputCommand(
        string verb,
        IReadOnlyList<string> args,
        string? selectedHandle)
    {
        return verb switch
        {
            "text" or "type" => ExecuteTextCommand(args, selectedHandle),
            "keys" or "key" => ExecuteKeysCommand(args, selectedHandle),
            _ => throw new ScriptCommandException("unsupported_command", $"Unsupported input command: {verb}")
        };
    }

    private ScriptCommandResult ExecuteTextCommand(IReadOnlyList<string> args, string? selectedHandle)
    {
        if (selectedHandle is not null)
        {
            _automation.FocusWindow(selectedHandle, out _);
        }

        var text = JoinArgs(args, 0) ?? string.Empty;
        if (!_automation.SendText(new TextInputRequest(text), out var error))
        {
            throw FromError(error, "text_failed");
        }

        return new ScriptCommandResult(new { sent = true, chars = text.Length });
    }

    private ScriptCommandResult ExecuteKeysCommand(IReadOnlyList<string> args, string? selectedHandle)
    {
        RequireArgs(args, 1, "keys requires a key chord.");
        if (selectedHandle is not null)
        {
            _automation.FocusWindow(selectedHandle, out _);
        }

        var keys = string.Join('+', args);
        if (!_automation.SendKeys(new KeyInputRequest(keys), out var error))
        {
            throw FromError(error, "keys_failed");
        }

        return new ScriptCommandResult(new { sent = true, keys });
    }
}
