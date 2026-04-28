using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private ScriptCommandResult ExecuteWindowCommand(
        string verb,
        IReadOnlyList<string> args,
        string? selectedHandle)
    {
        switch (verb)
        {
            case "focus":
                return ExecuteFocusCommand(selectedHandle);
            case "restore":
            case "maximize":
            case "minimize":
            case "hide":
            case "show":
                return ExecuteWindowStateCommand(verb, selectedHandle);
            case "move":
                return ExecuteMoveCommand(args, selectedHandle);
            case "close":
                return ExecuteCloseCommand(selectedHandle);
            default:
                throw new ScriptCommandException("unsupported_command", $"Unsupported window command: {verb}");
        }
    }

    private ScriptCommandResult ExecuteFocusCommand(string? selectedHandle)
    {
        var handle = RequireSelected(selectedHandle);
        if (!_automation.FocusWindow(handle, out var error))
        {
            throw FromError(error, "focus_failed");
        }

        return new ScriptCommandResult(new { focused = true, handle });
    }

    private ScriptCommandResult ExecuteWindowStateCommand(string verb, string? selectedHandle)
    {
        var handle = RequireSelected(selectedHandle);
        if (!_automation.SetWindowState(handle, new WindowStateRequest(verb), out var error))
        {
            throw FromError(error, "window_state_failed");
        }

        var window = _automation.TryGetWindow(handle, out var refreshed) ? refreshed : null;
        return new ScriptCommandResult(new { state = verb, handle }, handle, window);
    }

    private ScriptCommandResult ExecuteMoveCommand(IReadOnlyList<string> args, string? selectedHandle)
    {
        RequireArgs(args, 4, "move requires x y width height.");
        var handle = RequireSelected(selectedHandle);
        var move = new MoveWindowRequest(
            ParseInt(args[0], "x"),
            ParseInt(args[1], "y"),
            ParseInt(args[2], "width"),
            ParseInt(args[3], "height"));
        if (!_automation.MoveWindow(handle, move, out var error))
        {
            throw FromError(error, "move_failed");
        }

        var window = _automation.TryGetWindow(handle, out var refreshed) ? refreshed : null;
        return new ScriptCommandResult(new { moved = true, handle }, handle, window);
    }

    private ScriptCommandResult ExecuteCloseCommand(string? selectedHandle)
    {
        var handle = RequireSelected(selectedHandle);
        if (!_automation.CloseWindow(handle, out var error))
        {
            throw FromError(error, "close_failed");
        }

        return new ScriptCommandResult(new { closed = true, handle }, null);
    }
}
