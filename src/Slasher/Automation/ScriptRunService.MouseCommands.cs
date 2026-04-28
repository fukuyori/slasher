using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private ScriptCommandResult ExecuteMouseCommand(IReadOnlyList<string> args)
    {
        RequireArgs(args, 1, "mouse requires an action.");
        var action = args[0].ToLowerInvariant();
        return action switch
        {
            "move" => ExecuteMouseInputCommand("move", args.Skip(1).ToArray()),
            "click" => ExecuteMouseInputCommand("click", args.Skip(1).ToArray()),
            "doubleclick" or "double-click" => ExecuteMouseInputCommand("doubleclick", args.Skip(1).ToArray()),
            "rightclick" or "right-click" => ExecuteMouseInputCommand("click", ["right", .. args.Skip(1)]),
            "down" => ExecuteMouseInputCommand("down", args.Skip(1).ToArray()),
            "up" => ExecuteMouseInputCommand("up", args.Skip(1).ToArray()),
            "wheel" or "scroll" => ExecuteMouseWheelCommand(args.Skip(1).ToArray()),
            "drag" => ExecuteMouseDragCommand(args.Skip(1).ToArray()),
            "context" or "context-menu" or "menu" => ExecuteContextMenuCommand(args.Skip(1).ToArray()),
            _ => throw new ScriptCommandException(
                "unsupported_mouse_command",
                "mouse supports move, click, doubleclick, rightclick, down, up, wheel, drag, and context-menu.")
        };
    }

    private ScriptCommandResult ExecuteMouseInputCommand(string action, IReadOnlyList<string> args)
    {
        var (x, y, button) = ParseMousePointAndButton(action, args);

        if (!_automation.SendMouse(new MouseInputRequest(action, x, y, button), out var error))
        {
            throw FromError(error, "mouse_failed");
        }

        return new ScriptCommandResult(new { mouse = action, x, y, button });
    }

    private ScriptCommandResult ExecuteMouseWheelCommand(IReadOnlyList<string> args)
    {
        RequireArgs(args, 1, "mouse wheel requires a delta.");
        var delta = ParseInt(args[0], "delta");
        int? x = null;
        int? y = null;
        if (args.Count >= 3)
        {
            x = ParseInt(args[1], "x");
            y = ParseInt(args[2], "y");
        }

        if (!_automation.SendMouse(new MouseInputRequest("wheel", x, y, WheelDelta: delta), out var error))
        {
            throw FromError(error, "mouse_wheel_failed");
        }

        return new ScriptCommandResult(new { mouse = "wheel", delta, x, y });
    }

    private ScriptCommandResult ExecuteMouseDragCommand(IReadOnlyList<string> args)
    {
        RequireArgs(args, 4, "mouse drag syntax is: mouse drag <fromX> <fromY> <toX> <toY> [button] [durationMs] [steps].");
        var fromX = ParseInt(args[0], "fromX");
        var fromY = ParseInt(args[1], "fromY");
        var toX = ParseInt(args[2], "toX");
        var toY = ParseInt(args[3], "toY");
        var button = args.Count >= 5 ? NormalizeMouseButton(args[4]) : "left";
        var durationMs = args.Count >= 6 ? ParseInt(args[5], "durationMs") : 400;
        var steps = args.Count >= 7 ? ParseInt(args[6], "steps") : 24;

        var request = new MouseDragRequest(fromX, fromY, toX, toY, button, durationMs, steps);
        if (!_automation.DragMouse(request, out var error))
        {
            throw FromError(error, "mouse_drag_failed");
        }

        return new ScriptCommandResult(new { mouse = "drag", fromX, fromY, toX, toY, button, durationMs, steps });
    }

    private ScriptCommandResult ExecuteContextMenuCommand(IReadOnlyList<string> args)
    {
        RequireArgs(args, 2, "mouse context-menu syntax is: mouse context-menu <x> <y> [delayMs].");
        var x = ParseInt(args[0], "x");
        var y = ParseInt(args[1], "y");
        var delayMs = args.Count >= 3 ? ParseInt(args[2], "delayMs") : 250;
        if (!_automation.GetContextMenu(new ContextMenuRequest(x, y, delayMs), out var response, out var error) || response is null)
        {
            throw FromError(error, "context_menu_failed");
        }

        return new ScriptCommandResult(response, Screenshot: response.Screenshot, AssignmentValue: response);
    }

    private static (int? X, int? Y, string Button) ParseMousePointAndButton(string action, IReadOnlyList<string> args)
    {
        var button = "left";
        var values = args;
        if (values.Count > 0 && IsMouseButton(values[0]))
        {
            button = NormalizeMouseButton(values[0]);
            values = values.Skip(1).ToArray();
        }

        if (values.Count == 0)
        {
            return (null, null, button);
        }

        if (values.Count == 3 && IsMouseButton(values[2]))
        {
            button = NormalizeMouseButton(values[2]);
            values = values.Take(2).ToArray();
        }

        if (values.Count != 2)
        {
            throw new ScriptCommandException("invalid_mouse_arguments", $"mouse {action} syntax is: mouse {action} [x y] [button].");
        }

        return (ParseInt(values[0], "x"), ParseInt(values[1], "y"), button);
    }

    private static bool IsMouseButton(string value)
    {
        return value.Equals("left", StringComparison.OrdinalIgnoreCase)
            || value.Equals("right", StringComparison.OrdinalIgnoreCase)
            || value.Equals("middle", StringComparison.OrdinalIgnoreCase)
            || value.Equals("primary", StringComparison.OrdinalIgnoreCase)
            || value.Equals("secondary", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeMouseButton(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "primary" => "left",
            "secondary" => "right",
            "left" or "right" or "middle" => value.ToLowerInvariant(),
            _ => throw new ScriptCommandException("invalid_mouse_button", "Mouse button must be left, right, middle, primary, or secondary.")
        };
    }
}
