using Slasher.Api;
using Slasher.Windows;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private ScriptCommandResult ExecuteClipboardCommand(IReadOnlyList<string> args, string? selectedHandle)
    {
        RequireArgs(args, 1, "clipboard requires an action.");
        var action = args[0].ToLowerInvariant();
        return action switch
        {
            "assign" or "set" or "copy" => ExecuteClipboardAssign(args.Skip(1).ToArray()),
            "get" => ExecuteClipboardGet(),
            "paste" => ExecuteClipboardPaste(selectedHandle),
            "clear" => ExecuteClipboardClear(),
            _ => throw new ScriptCommandException("unsupported_clipboard_command", "clipboard supports assign, get, paste, and clear.")
        };
    }

    private ScriptCommandResult ExecuteClipboardAssign(IReadOnlyList<string> args)
    {
        var text = JoinArgs(args, 0) ?? string.Empty;
        _clipboard.SetText(text);
        return new ScriptCommandResult(new { assigned = true, chars = text.Length }, AssignmentValue: text);
    }

    private ScriptCommandResult ExecuteClipboardGet()
    {
        var text = _clipboard.GetText();
        return new ScriptCommandResult(new { text, chars = text.Length }, AssignmentValue: text);
    }

    private ScriptCommandResult ExecuteClipboardPaste(string? selectedHandle)
    {
        if (selectedHandle is not null)
        {
            _automation.FocusWindow(selectedHandle, out _);
        }

        if (!_automation.SendKeys(new KeyInputRequest("CTRL+V"), out var error))
        {
            throw FromError(error, "clipboard_paste_failed");
        }

        return new ScriptCommandResult(new { pasted = true });
    }

    private ScriptCommandResult ExecuteClipboardClear()
    {
        _clipboard.Clear();
        return new ScriptCommandResult(new { cleared = true });
    }
}
